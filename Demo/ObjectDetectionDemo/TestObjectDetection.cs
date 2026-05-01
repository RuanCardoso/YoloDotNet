// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2025 Niklas Swärd
// https://github.com/NickSwardh/YoloDotNet

using Accord.Statistics.Running;
using ClickableTransparentOverlay;
using Enjoy.ByteTrack;
using ImGuiNET;
using SkiaSharp;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using YoloDotNet;
using YoloDotNet.Core;
using YoloDotNet.Enums;
using YoloDotNet.Extensions;
using YoloDotNet.Models;
using YoloDotNet.Test.Common;
using YoloDotNet.Trackers;

using OpenCvSharp;
using SkiaSharp;

public static class MatExtensions
{
	/// <summary>
	/// Converte OpenCV Mat para SKBitmap (SkiaSharp)
	/// Mantém BGR → RGB automaticamente.
	/// </summary>
	public static SKBitmap ToSKBitmap(this Mat mat)
	{
		if (mat.Empty())
			return null;

		// OpenCV usa BGR, Skia usa RGBA
		Mat rgba = new();
		Cv2.CvtColor(mat, rgba, ColorConversionCodes.BGR2RGBA);

		var bmp = new SKBitmap(new SKImageInfo(rgba.Width, rgba.Height, SKColorType.Rgba8888));
		rgba.GetArray(out byte[] bytes);

		Marshal.Copy(bytes, 0, bmp.GetPixels(), bytes.Length);

		rgba.Dispose();
		return bmp;
	}


	/// <summary>
	/// Converte SKBitmap para Mat (BGR)
	/// Skia usa RGBA → convertendo para BGR para usar com OpenCV/YOLO.
	/// </summary>
	public static Mat ToMat(this SKBitmap bmp)
	{
		if (bmp == null || bmp.IsEmpty)
			return new Mat();

		int width = bmp.Width;
		int height = bmp.Height;

		Mat rgba = new(height, width, MatType.CV_8UC4);

		// Copia os bytes da Skia para o Mat
		var byteCount = width * height * 4;
		byte[] bytes = new byte[byteCount];
		Marshal.Copy(bmp.GetPixels(), bytes, 0, byteCount);
		rgba.SetArray(bytes);

		// RGBA → BGR (YOLO / OpenCV padrão)
		Mat bgr = new();
		Cv2.CvtColor(rgba, bgr, ColorConversionCodes.RGBA2BGR);

		rgba.Dispose();
		return bgr;
	}
}


namespace ObjectDetectionDemo
{
	using ObjectDetectionDemo.Enjoy.ByteTrack;
	using OpenCvSharp;
	using System.Collections.Generic;
	using System.Linq;

	namespace Enjoy.ByteTrack
	{
		public static class ObjectDetectionConverter
		{
			///// <summary>
			///// Converte uma lista de ObjectDetection para lista de Track (ByteTrack).
			///// </summary>
			//public static List<IObject> ToTracks(this IEnumerable<PoseEstimation> detections)
			//{
			//	var list = new List<IObject>();

			//	foreach (var det in detections)
			//		list.Add(det);

			//	return list;
			//}

			/// <summary>
			/// Converte uma lista de Track de volta para ObjectDetection.
			/// Mantém bounding box e score.
			/// Outros campos podem ser adicionados se necessário.
			/// </summary>
			public static List<PoseEstimation> ToDetections(this IEnumerable<Track> tracks)
			{
				var list = new List<PoseEstimation>();

				foreach (var track in tracks)
				{
					var rect = track.RectBox;

					var obj = new PoseEstimation
					{
						BoundingBox = new SKRectI(
							(int)rect.X,
							(int)rect.Y,
							(int)(rect.X + rect.Width),
							(int)(rect.Y + rect.Height)
						),

						Id = track.TrackId,
						Confidence = track.Score,
						Label = new LabelModel()
					};

					list.Add(obj);
				}

				return list;
			}
		}
	}


	public class Program
	{
		static void Main()
		{
			TestObjectDetection ob = new();
			ob.Initialize();
		}
	}


	/// <summary>
	/// Demonstrates object detection on static images using the YoloDotNet library.
	///
	/// This demo loads a sample image, runs object detection to detect axis-aligned bounding boxes,
	/// draws the results (bounding boxes, labels, confidence scores),
	/// and saves the processed image to disk.
	///
	/// It showcases:
	/// - Model initialization with configurable hardware and preprocessing options
	/// - Static image inference for detecting objects with standard bounding boxes
	/// - Customizable rendering of detection results, including labels, confidence scores, and boxes
	/// - Saving annotated output to disk
	/// - Console reporting of inference results
	///
	/// Execution providers:
	/// - CpuExecutionProvider: runs inference on CPU, universally supported but slower.
	/// - CudaExecutionProvider: uses NVIDIA GPU via CUDA for faster inference, with optional GPU warm-up.
	/// - TensorRtExecutionProvider: leverages NVIDIA TensorRT for highly optimized GPU inference with FP32, FP16, INT8
	///   precision modes, delivering significant speed improvements.
	///
	/// Important notes:
	/// - Choose the execution provider based on your hardware and performance requirements.
	/// - The demo creates an output folder on the desktop to store processed results.
	/// </summary>
	/// 

	internal class TestObjectDetection : Overlay
	{
		private static int width = 1920, height = 1080;
		private static float imgSize = 320f;
		private static int imgSizeInt = 320;


		private static KalmanFilter2D kf = new();
		private static System.Diagnostics.Stopwatch dtTimer;
		private static double lastTime = 0;

		private string _outputFolder = default!;
		private DetectionDrawingOptions? _drawingOptions;

		// get async key state
		[DllImport("user32.dll")]
		private static extern short GetAsyncKeyState(int vKey);

		List<PoseEstimation> detections = new();
		int frameCount = 0;
		double fpsTimer = 0.0;
		double currentFPS = 0.0;

		private static bool toggleState = false;
		private static bool lastTogglePressed = false;

		// Ajustes
		public static int RecoilStrength = 0;   // força do recoil
		public static float MouseSpeed = 5f;       // velocidade do mouse

		// Teclas configuráveis
		// Teclas
		private const int KEY_TOGGLE = 0x70; // F1

		// Recoil (+ / -)
		private const int KEY_RECOIL_UP = 0xBB;   // VK_OEM_PLUS  (+)
		private const int KEY_RECOIL_DOWN = 0xBD; // VK_OEM_MINUS (-)

		// Velocidade do mouse (setas)
		private const int KEY_SPEED_UP = 0x26;   // UP arrow
		private const int KEY_SPEED_DOWN = 0x28; // DOWN arrow

		public static bool IsActive => toggleState;

		/// <summary>
		/// MODO 1: Verifica se o CENTRO da bbox está dentro do FOV circular (RECOMENDADO)
		/// </summary>
		public static List<PoseEstimation> FilterByFov2(
			List<PoseEstimation> detections,
			int imgSizeInt,
			float fovSize = 640f)
		{
			if (detections == null || detections.Count == 0)
				return new List<PoseEstimation>();

			float centerX = imgSizeInt / 2.0f;
			float centerY = imgSizeInt / 2.0f;
			float r = fovSize / 2f;
			float radiusSquared = r * r;

			return detections.Where(det =>
			{
				SKRectI bbox = det.BoundingBox;

				// Verifica apenas o CENTRO da bbox
				float deltaX = bbox.MidX - centerX;
				float deltaY = bbox.MidY - centerY;
				float distanceSquared = deltaX * deltaX + deltaY * deltaY;

				return distanceSquared <= radiusSquared;

			}).ToList();
		}

		public static List<PoseEstimation> FilterByFov(
		  List<PoseEstimation> detections,
		int imgSizeInt,
		float fovSize = 640f)
		{
			if (detections == null || detections.Count == 0)
				return [];

			float centerX = imgSizeInt / 2.0f;
			float centerY = imgSizeInt / 2.0f;
			float r = fovSize / 2f;
			float radiusSquared = r * r;

			static bool IsPointInCircle(float pointX, float pointY, float centerX, float centerY, float radiusSquared)
			{
				float deltaX = pointX - centerX;
				float deltaY = pointY - centerY;
				return (deltaX * deltaX + deltaY * deltaY) <= radiusSquared;
			}

			return detections.Where(det =>
			{
				SKRectI bbox = det.BoundingBox;

				// Verifica os 4 cantos da bbox
				bool topLeftInside = IsPointInCircle(bbox.Left, bbox.Top, centerX, centerY, radiusSquared);
				bool topRightInside = IsPointInCircle(bbox.Right, bbox.Top, centerX, centerY, radiusSquared);
				bool bottomLeftInside = IsPointInCircle(bbox.Left, bbox.Bottom, centerX, centerY, radiusSquared);
				bool bottomRightInside = IsPointInCircle(bbox.Right, bbox.Bottom, centerX, centerY, radiusSquared);

				// Todos os cantos devem estar dentro
				return topLeftInside && topRightInside && bottomLeftInside && bottomRightInside;

			}).ToList();
		}


		ByteTracker bt = new ByteTracker(frameRate: 200, trackBuffer: 6, trackThresh: 0.5f, matchThresh: 0.7f, highThresh: 0.7f);

		public static Mat Sharpen(Mat img)
		{
			Mat blur = new();
			Cv2.GaussianBlur(img, blur, new Size(0, 0), 1.0);
			Cv2.AddWeighted(img, 1.5, blur, -0.5, 0, img);
			return img;
		}

		public static SKBitmap Sharpen(SKBitmap src, float amount = 1.5f, float blur = 1.0f)
		{
			int w = src.Width;
			int h = src.Height;

			SKBitmap blurred = new SKBitmap(w, h);
			using (var canvas = new SKCanvas(blurred))
			{
				// blur Gaussian
				var paint = new SKPaint
				{
					ImageFilter = SKImageFilter.CreateBlur(blur, blur),
					IsAntialias = false
				};
				canvas.DrawBitmap(src, 0, 0, paint);
			}

			SKBitmap dst = new SKBitmap(w, h);

			// unsharp mask: dst = src * amount - blurred * (amount - 1)
			float a = amount;
			float b = -(amount - 1f);

			unsafe
			{
				uint* pSrc = (uint*)src.GetPixels();
				uint* pBlur = (uint*)blurred.GetPixels();
				uint* pDst = (uint*)dst.GetPixels();

				int total = w * h;

				for (int i = 0; i < total; i++)
				{
					uint c1 = pSrc[i];
					uint c2 = pBlur[i];

					byte r1 = (byte)((c1 >> 16) & 255);
					byte g1 = (byte)((c1 >> 8) & 255);
					byte b1 = (byte)(c1 & 255);

					byte r2 = (byte)((c2 >> 16) & 255);
					byte g2 = (byte)((c2 >> 8) & 255);
					byte b2 = (byte)(c2 & 255);

					int r = (int)(r1 * a + r2 * b);
					int g = (int)(g1 * a + g2 * b);
					int b_ = (int)(b1 * a + b2 * b);

					r = Math.Clamp(r, 0, 255);
					g = Math.Clamp(g, 0, 255);
					b_ = Math.Clamp(b_, 0, 255);

					pDst[i] = (uint)(0xFF000000 | (r << 16) | (g << 8) | b_);
				}
			}

			return dst;
		}

		public static SKBitmap ApplyGamma(SKBitmap src, double gamma = 1.5)
		{
			SKBitmap dst = new SKBitmap(src.Width, src.Height);

			byte[] lut = new byte[256];
			double inv = 1.0 / gamma;
			for (int i = 0; i < 256; i++)
				lut[i] = (byte)(Math.Pow(i / 255.0, inv) * 255);

			unsafe
			{
				uint* pSrc = (uint*)src.GetPixels();
				uint* pDst = (uint*)dst.GetPixels();

				int total = src.Width * src.Height;

				for (int i = 0; i < total; i++)
				{
					uint c = pSrc[i];

					byte r = (byte)((c >> 16) & 255);
					byte g = (byte)((c >> 8) & 255);
					byte b = (byte)(c & 255);

					byte rr = lut[r];
					byte gg = lut[g];
					byte bb = lut[b];

					pDst[i] = (uint)(0xFF000000 | (rr << 16) | (gg << 8) | bb);
				}
			}

			return dst;
		}

		// Importação da função nativa do Windows
		[DllImport("user32.dll")]
		private static extern int GetSystemMetrics(int nIndex);

		// Índices para largura (0) e altura (1) da tela principal
		private const int SM_CXSCREEN = 0;
		private const int SM_CYSCREEN = 1;

		public void Initialize()
		{
			width = GetSystemMetrics(SM_CXSCREEN);
			height = GetSystemMetrics(SM_CYSCREEN);

			Start();
			_drawingOptions = new DetectionDrawingOptions()
			{
				DrawLabels = true,
				DrawConfidenceScore = true,
				BorderThickness = 2f,
				BoundingBoxHexColors = new[] { "#FF0000", "#00FF00", "#0000FF" }
			};

			// Initialize YoloDotNet.
			// YoloOptions configures the model, hardware settings, and image processing behavior.
			using var yolo = new Yolo(new YoloOptions
			{
				// Path or byte[] to the ONNX model file. 
				// SharedConfig.GetTestModelV11 loads a YOLOv11 model.
				OnnxModel = SharedConfig.GetTestModel("yolov8n-pose.onnx"),

				// Select execution provider (determines how and where inference is executed).
				// Available execution providers:
				//
				//   - CpuExecutionProvider()  
				//     Runs inference entirely on the CPU.
				//     Universally compatible but generally the slowest option.
				//
				//   - CudaExecutionProvider(GpuId: 0, PrimeGpu: true)  
				//     Executes inference on an NVIDIA GPU using CUDA.
				//     Optionally primes the GPU with a warm-up run to reduce first-inference latency.
				//
				//   - TensorRtExecutionProvider() { ... }
				//     Executes inference using NVIDIA TensorRT for highly optimized GPU acceleration.
				//     Supports FP32 and FP16 precision modes, and optionally INT8 if calibration data is provided.
				//     Offers significant speed-ups by leveraging TensorRT engine optimizations.
				//
				//     See the TensorRTDemo and documentation for detailed configuration and best practices.
				ExecutionProvider = new DmlExecutionProvider(),

				// Resize mode applied before inference. Proportional maintains the aspect ratio (adds padding if needed),
				// while Stretch resizes the image to fit the target size without preserving the aspect ratio.
				// Set this accordingly, as it directly impacts the inference results.
				ImageResize = ImageResize.Proportional,
			});

			// Print model type
			Console.WriteLine($"Loaded ONNX Model: {yolo.ModelInfo}");

			// Load input image as SKBitmap (or SKImage)
			// The image is sourced from SharedConfig for test/demo purposes.

			int cropW = imgSizeInt;
			int cropH = imgSizeInt;

			int left = (width / 2) - (cropW / 2);
			int top = (height / 2) - (cropH / 2);

			dtTimer = System.Diagnostics.Stopwatch.StartNew();
			SortTracker tracker = new();
			while (true)
			{

				// ---------------------------
				// 1) TOGGLE ON/OFF (F1)
				// ---------------------------
				bool togglePressed = (GetAsyncKeyState(KEY_TOGGLE) & 1) != 0;
				if (togglePressed && !lastTogglePressed)
				{
					toggleState = !toggleState;
					Console.WriteLine("AIM: " + (toggleState ? "LIGADO" : "DESLIGADO"));
				}
				lastTogglePressed = togglePressed;

				// ---------------------------
				// 2) ADJUSTE DE RECOIL (F2/F3)
				// ---------------------------
				if ((GetAsyncKeyState(KEY_RECOIL_UP) & 1) != 0)
				{
					RecoilStrength += 1;
					Console.WriteLine("Recoil Strength: " + RecoilStrength.ToString("0.0"));
				}
				if ((GetAsyncKeyState(KEY_RECOIL_DOWN) & 1) != 0)
				{
					RecoilStrength -= 1;
					Console.WriteLine("Recoil Strength: " + RecoilStrength.ToString("0.0"));
				}

				// ---------------------------
				// 3) ADJUSTE DE SPEED (F5/F6)
				// ---------------------------
				if ((GetAsyncKeyState(KEY_SPEED_UP) & 1) != 0)
				{
					MouseSpeed += 0.1f;
					Console.WriteLine("Mouse Speed: " + MouseSpeed.ToString("0.0"));
				}
				if ((GetAsyncKeyState(KEY_SPEED_DOWN) & 1) != 0)
				{
					MouseSpeed -= 0.1f;
					Console.WriteLine("Mouse Speed: " + MouseSpeed.ToString("0.0"));
				}

				// calcular dt manualmente
				double now = dtTimer.Elapsed.TotalSeconds;
				double dt = now - lastTime;
				lastTime = now;

				frameCount++;
				fpsTimer += dt;

				if (fpsTimer >= 1.0)
				{
					currentFPS = frameCount;

					frameCount = 0;
					fpsTimer -= 1.0;
				}

				try
				{
					//var image = CaptureManager.DX11Capture(
					//	new(left, top, cropW, cropH)
					//);

					//if (image is null)
					//{
					//	Thread.Sleep(15);
					//	continue;
					//}

					//// rodar YOLO
					//detections = yolo.RunPoseEstimation(image, confidence: 0.3, iou: 0.7); // Head

					// using (PerfScope.Create("Inference"))
					{
						//if (GetAsyncKeyState(0x1B))
						//	break;

						// Run object detection inference
						// load from path

						var image = CaptureManager.DX11Capture(
							new(left, top, cropW, cropH)
						);

						if (!IsActive)
						{
							continue;
						}

						if (image is null)
						{
							continue;
						}

						image = Sharpen(image);
						// image = ApplyGamma(image);
						// rodar YOLO
						detections = yolo.RunPoseEstimation(image, confidence: 0.5, iou: 0.5).ToList(); // Head
																										// detections = bt.Update(detections.ToTracks()).ToDetections();

						//if (detections == null || detections.Count == 0)
						//	detections = bt.Predict().ToDetections();
						//else detections = bt.Update(detections.ToTracks()).ToDetections();

						// detections = FilterByFov2(detections, imgSizeInt);
						var center = GetClosestToCenter(detections);

						if (!IsLeftMouseDown())
							continue;

						MoveMouseRelative(0, RecoilStrength); // -> recoil control

						if (center != null && center.KeyPoints != null && center.KeyPoints.Length > 0)
						{
							var kps = center.KeyPoints;

							// Keypoints COCO head:
							// 0 = nose
							// 1 = left_eye
							// 2 = right_eye

							float headX = 0, headY = 0;

							bool hasEyes =
								kps.Length > 2 &&
								kps[1].Confidence > 0.3f &&
								kps[2].Confidence > 0.3f;

							if (hasEyes)
							{
								// MÉTODO 1: Média dos olhos → melhor precisão
								headX = (kps[1].X + kps[2].X) / 2f;
								headY = (kps[1].Y + kps[2].Y) / 2f;
							}
							else if (kps.Length > 0 && kps[0].Confidence > 0.3f)
							{
								// MÉTODO 2 (fallback): Nariz
								headX = kps[0].X;
								headY = kps[0].Y;
							}
							else // Fallback body center
							{
								var bbox = center.BoundingBox;
								int detectedX = (int)((bbox.Left + bbox.Width / 2) * (width / imgSize));
								int detectedY = (int)((bbox.Top + bbox.Height / 2) * (height / imgSize));
								MoveCrosshair(detectedX, detectedY, 10);
								continue;
							}

							int hDetectedX = (int)(headX * (width / imgSize));
							int hDetectedY = (int)(headY * (height / imgSize));

							MoveCrosshair(hDetectedX, hDetectedY, 10);
						}

						double smoothX, smoothY;
						const float MIN_CONF = 0.35f;

						//// TEM detecção → Update()
						//if (center != null && center.KeyPoints != null && center.KeyPoints.Length > 0)
						//{
						//	var kps = center.KeyPoints;

						//	// Keypoints COCO head:
						//	// 0 = nose
						//	// 1 = left_eye
						//	// 2 = right_eye

						//	float headX = 0, headY = 0;


						//	// 1) HEAD-TOP (kp4) — melhor ponto para aim assist
						//	if (kps.Length > 4 && kps[4].Confidence > MIN_CONF)
						//	{
						//		headX = kps[4].X;
						//		headY = kps[4].Y;
						//	}
						//	// 2) Olhos (kp1, kp2)
						//	else if (
						//		kps.Length > 2 &&
						//		kps[1].Confidence > MIN_CONF &&
						//		kps[2].Confidence > MIN_CONF)
						//	{
						//		headX = (kps[1].X + kps[2].X) / 2f;
						//		headY = (kps[1].Y + kps[2].Y) / 2f;
						//	}
						//	// 3) Nariz (kp0)
						//	else if (kps.Length > 0 && kps[0].Confidence > MIN_CONF)
						//	{
						//		headX = kps[0].X;
						//		headY = kps[0].Y;
						//	}
						//	// 4) Fallback: centro da bbox
						//	else
						//	{
						//		var bbox = center.BoundingBox;
						//		int detectedX = (int)((bbox.Left + bbox.Width / 2) * (width / imgSize));
						//		int detectedY = (int)((bbox.Top + bbox.Height / 2) * (height / imgSize));
						//		MoveCrosshair(detectedX, detectedY, 10);
						//		continue;
						//	}

						//	int hDetectedX = (int)(headX * (width / imgSize));
						//	int hDetectedY = (int)(headY * (height / imgSize));

						//	MoveCrosshair(hDetectedX, hDetectedY, 10);
						//}

						// TEM detecção → Update()
						// TEM detecção → Update()
						//if (center != null && center.KeyPoints != null && center.KeyPoints.Length > 0)
						//{
						//	var kps = center.KeyPoints;
						//	float targetX;
						//	float targetY;

						//	// PRIORIDADE 1: Ombros + Quadris (MELHOR)
						//	if (kps.Length > 12 &&
						//		kps[5].Confidence > 0.5f && kps[6].Confidence > 0.5f &&
						//		kps[11].Confidence > 0.5f && kps[12].Confidence > 0.5f)
						//	{
						//		float shoulderX = (kps[5].X + kps[6].X) / 2f;
						//		float shoulderY = (kps[5].Y + kps[6].Y) / 2f;
						//		float hipX = (kps[11].X + kps[12].X) / 2f;
						//		float hipY = (kps[11].Y + kps[12].Y) / 2f;

						//		targetX = (shoulderX + hipX) / 2f;
						//		targetY = (shoulderY + hipY) / 2f;
						//	}
						//	// PRIORIDADE 2: Só Ombros
						//	else if (kps.Length > 6 &&
						//			 kps[5].Confidence > 0.5f && kps[6].Confidence > 0.5f)
						//	{
						//		targetX = (kps[5].X + kps[6].X) / 2f;
						//		targetY = (kps[5].Y + kps[6].Y) / 2f;

						//		// Offset para baixo (peito)
						//		if (kps.Length > 0 && kps[0].Confidence > 0.5f)
						//			targetY += Math.Abs(targetY - kps[0].Y) * 0.4f;
						//		else
						//			targetY += 15f;
						//	}
						//	// PRIORIDADE 3: BBox center
						//	else
						//	{
						//		var bbox = center.BoundingBox;
						//		targetX = bbox.Left + bbox.Width / 2f;
						//		targetY = bbox.Top + bbox.Height / 2f;
						//	}

						//	int detectedX = (int)(targetX * (width / imgSize));
						//	int detectedY = (int)(targetY * (height / imgSize));

						//	MoveCrosshair(detectedX, detectedY, 10);
						//}
					}
				}
				catch { }
			}
		}

		[DllImport("user32.dll")]
		private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

		public static void MoveMouseRelative(int dx, int dy)
		{
			mouse_event(0x0001, dx, dy, 0, 0);
		}

		private const int VK_LBUTTON = 0x01;
		private const int VK_RBUTTON = 0x02;

		/// <summary>
		/// Returns true if left mouse button (shoot) is pressed.
		/// </summary>
		public static bool IsLeftMouseDown()
		{
			return (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
		}

		public static void MoveCrosshair(int detectedX, int detectedY, double mouseSpeed)
		{
			mouseSpeed = MouseSpeed;
			int screenCenterX = width / 2;
			int screenCenterY = height / 2;

			// Diferença entre o alvo detectado e o centro da tela
			int rawX = detectedX - screenCenterX;
			int rawY = detectedY - screenCenterY;

			// Correção opcional de aspecto (igual seu código)
			// double aspectCorrection = (double)ScreenController.ScreenWidth / ScreenController.ScreenHeight;
			// rawY = (int)(rawY / aspectCorrection);

			// Aplicar sensibilidade (igual ao Python: divide)
			int moveX = (int)(rawX / mouseSpeed);
			int moveY = (int)(rawY / mouseSpeed);

			// Move exatamente como o Python faz
			// (moveX, moveY);

			MoveMouseRelative(moveX, moveY);
		}

		public static PoseEstimation? GetClosestToCenter(List<PoseEstimation> detections)
		{
			if (detections == null || detections.Count == 0)
				return null;

			PoseEstimation? best = null;
			float bestDist = float.MaxValue;
			float center = imgSize / 2.0f;

			foreach (var det in detections)
			{
				var rect = det.BoundingBox;

				var dx = rect.Left * imgSize - center;
				var dy = rect.Top * imgSize - center;
				float d2 = dx * dx + dy * dy; // dx^2 + dy^2
											  // sem sqrt → mais rápido, mesmo resultado para comparar
				if (d2 < bestDist)
				{
					bestDist = d2;
					best = det;
				}
			}

			return best;
		}


		private const int HWND_TOPMOST = -1;
		private const int HWND_NOTOPMOST = -2;

		private const uint SWP_NOMOVE = 0x0002;
		private const uint SWP_NOSIZE = 0x0001;
		private const uint SWP_SHOWWINDOW = 0x0040;

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool SetWindowPos(
			IntPtr hWnd,
			IntPtr hWndInsertAfter,
			int X,
			int Y,
			int cx,
			int cy,
			uint uFlags);

		public static void MakeTopMost(IntPtr handle)
		{
			SetWindowPos(
				handle,
				new IntPtr(HWND_TOPMOST),
				0, 0, 0, 0,
				SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
		}

		double[,] ConvertToByteTrackArray(List<PoseEstimation> detections)
		{
			int n = detections.Count;
			double[,] output = new double[n, 6];

			for (int i = 0; i < n; i++)
			{
				var d = detections[i];
				var box = d.BoundingBox;

				output[i, 0] = box.Left;                 // x1
				output[i, 1] = box.Top;                  // y1
				output[i, 2] = box.Right;                // x2
				output[i, 3] = box.Bottom;               // y2
				output[i, 4] = d.Confidence;             // score
				output[i, 5] = d.Label.Index;               // classId (ou coloque 0 se quiser tudo inimigo)
			}

			return output;
		}


		protected override Task PostInitialized()
		{
			// MakeTopMost(window.Handle);

			Size = new(width, height);
			return Task.CompletedTask;
		}

		protected override void Render()
		{
			if (!IsActive)
			{
				return;
			}

			var style = ImGui.GetStyle();

			ImGui.SetNextWindowSize(new(width, height));
			ImGui.SetNextWindowPos(new(0, 0));

			ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, 0f));

			ImGui.Begin("TEST OB",
				ImGuiWindowFlags.NoInputs |
				ImGuiWindowFlags.NoTitleBar |
				ImGuiWindowFlags.NoResize |
				ImGuiWindowFlags.NoMove |
				ImGuiWindowFlags.NoScrollbar |
				ImGuiWindowFlags.NoScrollWithMouse);

			var background = ImGui.GetBackgroundDrawList();

			int offsetX = (width / 2) - (imgSizeInt / 2);
			int offsetY = (height / 2) - (imgSizeInt / 2);

			var detections = this.detections.ToList();
			if (detections != null && detections.Count > 0)
			{
				foreach (var r in detections)
				{
					if (r?.KeyPoints == null || r.KeyPoints.Length == 0)
						continue;

					uint skeletonColor = ImGui.GetColorU32(new Vector4(1f, 0f, 0f, 1f));

					// Skeleton simplificado
					int[][] skeleton = new int[][]
					{
                // Tronco
                new int[] {5, 6},
				new int[] {5, 11},
				new int[] {6, 12},
				new int[] {11, 12},

                // Braço esquerdo
                new int[] {5, 7},
				new int[] {7, 9},

                // Braço direito
                new int[] {6, 8},
				new int[] {8, 10},

                // Perna esquerda
                new int[] {11, 13},
				new int[] {13, 15},

                // Perna direita
                new int[] {12, 14},
				new int[] {14, 16}
					};

					foreach (var connection in skeleton)
					{
						int idx1 = connection[0];
						int idx2 = connection[1];

						if (idx1 < r.KeyPoints.Length && idx2 < r.KeyPoints.Length)
						{
							var kp1 = r.KeyPoints[idx1];
							var kp2 = r.KeyPoints[idx2];

							if (kp1.Confidence > 0.5f && kp2.Confidence > 0.5f)
							{
								Vector2 p1 = new(kp1.X + offsetX, kp1.Y + offsetY);
								Vector2 p2 = new(kp2.X + offsetX, kp2.Y + offsetY);

								background.AddLine(p1, p2, skeletonColor, 2f);
							}
						}
					}
				}
			}

			// ===== FPS Overlay =====
			string fpsText = $"FPS: {currentFPS:F0} - Recoil Force: {RecoilStrength} - Mouse Speed: {MouseSpeed}";
			Vector2 fpsTextSize = ImGui.CalcTextSize(fpsText);

			float padding = 10f;
			Vector2 fpsPos = new Vector2(padding, height - fpsTextSize.Y - padding);

			Vector2 bgMin = new Vector2(fpsPos.X - 5, fpsPos.Y - 3);
			Vector2 bgMax = new Vector2(fpsPos.X + fpsTextSize.X + 5, fpsPos.Y + fpsTextSize.Y + 3);

			uint colorBg = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.8f));
			background.AddRectFilled(bgMin, bgMax, colorBg);

			uint colorWhite = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
			background.AddText(fpsPos, colorWhite, fpsText);

			ImGui.End();
			ImGui.PopStyleColor();
		}
	}
}
