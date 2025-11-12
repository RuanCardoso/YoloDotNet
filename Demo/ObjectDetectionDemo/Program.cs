// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2025 Niklas Swärd
// https://github.com/NickSwardh/YoloDotNet

using SkiaSharp;
using System.Diagnostics;
using System.Globalization;
using YoloDotNet;
using YoloDotNet.Core;
using YoloDotNet.Enums;
using YoloDotNet.Extensions;
using YoloDotNet.Models;
using YoloDotNet.Test.Common;
using YoloDotNet.Test.Common.Enums;

namespace ObjectDetectionDemo
{
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
	internal class Program
	{
		private static string _outputFolder = default!;
		private static DetectionDrawingOptions? _drawingOptions;

		static void Main(string[] args)
		{
			_drawingOptions = new DetectionDrawingOptions()
			{
				DrawLabels = false,
				TailThickness = 1f,
				BorderThickness = 1f
			};

			CreateOutputFolder();

			// Initialize YoloDotNet.
			// YoloOptions configures the model, hardware settings, and image processing behavior.
			using var yolo = new Yolo(new YoloOptions
			{
				// Path or byte[] to the ONNX model file. 
				// SharedConfig.GetTestModelV11 loads a YOLOv11 model.
				OnnxModel = SharedConfig.GetTestModel("pubg.onnx"),

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
				ExecutionProvider = new CudaExecutionProvider(),

				// Resize mode applied before inference. Proportional maintains the aspect ratio (adds padding if needed),
				// while Stretch resizes the image to fit the target size without preserving the aspect ratio.
				// Set this accordingly, as it directly impacts the inference results.
				ImageResize = ImageResize.Proportional,

				// Sampling options for resizing; affects inference speed and quality.
				// For examples of other sampling options, see benchmarks: https://github.com/NickSwardh/YoloDotNet/tree/master/test/YoloDotNet.Benchmarks
				SamplingOptions = new(SKFilterMode.Nearest) // YoloDotNet default
			});

			// Print model type
			Console.WriteLine($"Loaded ONNX Model: {yolo.ModelInfo}");

			// Load input image as SKBitmap (or SKImage)
			// The image is sourced from SharedConfig for test/demo purposes.
			using var image = SKBitmap.Decode(SharedConfig.GetTestImage("inv.jpg"));

			List<ObjectDetection>? results = null;
			PerfScope.Run("Object Detection", 60, () =>
			{
				// Run object detection inference
				results = yolo.RunObjectDetection(image, confidence: 0.6, iou: 0.7);
			});

			if (results is null)
				return;

			// Draw results
			image.Draw(results, _drawingOptions);

			// If using SKImage, the Draw method returns a new SKBitmap with the drawn results.
			// Example:
			// using var resultImage = image.Draw(results, _drawingOptions);

			// Save image
			var fileName = Path.Combine(_outputFolder, "ObjectDetection.jpg");
			image.Save(fileName, SKEncodedImageFormat.Jpeg, 80);

			PrintResults(results);
			DisplayOutputFolder();
		}

		private static void PrintResults(List<ObjectDetection> results)
		{
			Console.WriteLine();
			Console.WriteLine($"Inference Results: {results.Count} objects");
			Console.WriteLine(new string('=', 80));

			Console.ForegroundColor = ConsoleColor.Blue;

			foreach (var result in results)
			{
				var label = result.Label.Name;
				var confidence = (result.Confidence * 100).ToString("0.##", CultureInfo.InvariantCulture);
				Console.WriteLine($"{label} ({confidence}%)");
			}

			Console.ForegroundColor = ConsoleColor.Gray;
		}

		private static void CreateOutputFolder()
		{
			_outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "YoloDotNet_Results");

			if (Directory.Exists(_outputFolder) is false)
				Directory.CreateDirectory(_outputFolder);
		}

		private static void DisplayOutputFolder()
			=> Process.Start("explorer.exe", _outputFolder);
	}
}
