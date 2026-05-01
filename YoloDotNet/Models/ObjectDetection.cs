// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2023-2025 Niklas Swärd
// https://github.com/NickSwardh/YoloDotNet

using Enjoy.ByteTrack;

namespace YoloDotNet.Models
{
    /// <summary>
    /// Represents the result of object detection, including label information, confidence score, and bounding box.
    /// </summary>
    public class ObjectDetection : TrackingInfo, IDetection, IObject
    {
        /// <summary>
        /// Label information associated with the detected object.
        /// </summary>
        public LabelModel Label { get; init; } = new();

        /// <summary>
        /// Confidence score of the detected object.
        /// </summary>
        public double Confidence { get; init; }

        public float Dist { get; set; }

        /// <summary>
        /// Rectangle defining the region of interest (bounding box) of the detected object.
        /// </summary>
        public SKRectI BoundingBox { get; init; }

		// ================================
		// IObject IMPLEMENTATION
		// ================================

		// RectBox que o ByteTrack usa
		public RectBox RectBox =>
			new RectBox(
				BoundingBox.Left,
				BoundingBox.Top,
				BoundingBox.Width,
				BoundingBox.Height
			);

		// Probabilidade do YOLO (float)
		public float Prob => (float)Confidence;

		// ID numérico da classe
		int IObject.Label => Label.Index; // ou 0 caso não possua ID

		public bool IsActiveted { get; set; }
		public TrackState State { get; set; }

		// Converte para Track do ByteTrack
		public Track ToTrack()
		{
			// Track(RectBox rectBox, float score)
			return new Track(RectBox, Prob);
		}
	}
}
