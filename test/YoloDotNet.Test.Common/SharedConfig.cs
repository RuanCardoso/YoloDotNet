// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2024-2025 Niklas Swärd
// https://github.com/NickSwardh/YoloDotNet

namespace YoloDotNet.Test.Common
{
	using System;
	using System.IO;

	using YoloDotNet.Enums;
	using YoloDotNet.Test.Common.Enums;

	public static class SharedConfig
	{
		public const string BaseModels = "Assets\\Models";
		public const string BaseMedia = "Assets\\Media";
		public const string BaseCache = "Assets\\Cache";

		/// <summary>
		/// Get test model by name
		/// </summary>
		/// <param name="modelName"></param>
		/// <returns></returns>
		public static string GetTestModel(string modelName) => Path.Join(BaseModels, modelName);

		/// <summary>
		/// Test models for Yolo V5U
		/// </summary>
		/// <param name="modelType"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static string GetTestModelV5U(ModelType modelType) => modelType switch
		{
			ModelType.ObjectDetection => Path.Join(BaseModels, "yolov5su.onnx"),
			_ => throw new ArgumentException("Unknown modeltype.")
		};

		/// <summary>
		/// Test models for Yolo V8
		/// </summary>
		/// <param name="modelType"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static string GetTestModelV8(ModelType modelType) => modelType switch
		{
			ModelType.Classification => Path.Join(BaseModels, "yolov8s-cls.onnx"),
			ModelType.ObjectDetection => Path.Join(BaseModels, "yolov8s.onnx"),
			ModelType.ObbDetection => Path.Join(BaseModels, "yolov8s-obb.onnx"),
			ModelType.Segmentation => Path.Join(BaseModels, "yolov8s-seg.onnx"),
			ModelType.PoseEstimation => Path.Join(BaseModels, "yolov8s-pose.onnx"),
			_ => throw new ArgumentException("Unknown modeltype.")
		};

		/// <summary>
		/// Test models for Yolo V9
		/// </summary>
		/// <param name="modelType"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static string GetTestModelV9(ModelType modelType) => modelType switch
		{
			ModelType.ObjectDetection => Path.Join(BaseModels, "yolov9s.onnx"),
			_ => throw new ArgumentException("Unknown modeltype.")
		};

		/// <summary>
		/// Test models for Yolo V10
		/// </summary>
		/// <param name="modelType"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static string GetTestModelV10(ModelType modelType) => modelType switch
		{
			ModelType.ObjectDetection => Path.Join(BaseModels, "yolov10s.onnx"),
			_ => throw new ArgumentException("Unknown modeltype.")
		};

		/// <summary>
		/// Test models for Yolo V11
		/// </summary>
		/// <param name="modelType"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static string GetTestModelV11(ModelType modelType) => modelType switch
		{
			ModelType.Classification => Path.Join(BaseModels, "yolov11s-cls.onnx"),
			ModelType.ObjectDetection => Path.Join(BaseModels, "yolov11s.onnx"),
			ModelType.ObbDetection => Path.Join(BaseModels, "yolov11s-obb.onnx"),
			ModelType.Segmentation => Path.Join(BaseModels, "yolov11s-seg.onnx"),
			ModelType.PoseEstimation => Path.Join(BaseModels, "yolov11s-pose.onnx"),
			_ => throw new ArgumentException("Unknown modeltype.")
		};

		/// <summary>
		/// Test models for Yolo V12
		/// </summary>
		/// <param name="modelType"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static string GetTestModelV12(ModelType modelType) => modelType switch
		{
			ModelType.ObjectDetection => Path.Join(BaseModels, "yolov12s.onnx"),
			_ => throw new ArgumentException("Unknown modeltype.")
		};

		/// <summary>
		/// Test images
		/// </summary>
		/// <param name="imageType"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static string GetTestImage(ImageType imageType) => imageType switch
		{
			ImageType.Hummingbird => Path.Join(BaseMedia, "hummingbird.jpg"),
			ImageType.Street => Path.Join(BaseMedia, "street.jpg"),
			ImageType.People => Path.Join(BaseMedia, "people.jpg"),
			ImageType.Crosswalk => Path.Join(BaseMedia, "crosswalk.jpg"),
			ImageType.Island => Path.Join(BaseMedia, "island.jpg"),
			_ => throw new ArgumentException("Unknown ImageType.")
		};

		/// <summary>
		/// Get test image by name
		/// </summary>
		/// <param name="imageName"></param>
		/// <returns></returns>
		public static string GetTestImage(string imageName) => Path.Join(BaseMedia, imageName);

		/// <summary>
		/// Test Videos
		/// </summary>
		/// <param name="videoType"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static string GetTestVideo(VideoType videoType) => videoType switch
		{
			VideoType.PeopleWalking => Path.Join(BaseMedia, "walking.mp4"),
			_ => throw new ArgumentException("Unknown VideoType.")
		};
	}
}
