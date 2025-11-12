/// <summary>
/// Provides a lightweight performance measurement scope that automatically measures 
/// execution time and calculates the equivalent FPS (frames per second) when disposed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Usage:</b> 
/// Use the <see cref="Create(string)"/> factory method together with a <c>using</c> statement 
/// to automatically measure the execution time of a code block.
/// </para>
/// <code>
/// using (PerfScope.Create("AI Update"))
/// {
///     UpdateAI();
/// }
/// </code>
/// <para>
/// When the scope is disposed, the elapsed time (in milliseconds) and the calculated FPS are 
/// printed to the console output.
/// </para>
/// </remarks>
public sealed class PerfScope : IDisposable
{
	private readonly Stopwatch? _watch;
	private readonly string? _label;

	private PerfScope()
	{
		// Intentionally left blank
	}

	private PerfScope(string label = "Benchmark")
	{
		_label = label;
		_watch = Stopwatch.StartNew();
	}

	/// <summary>
	/// Creates and starts a new <see cref="PerfScope"/> instance for measuring performance.
	/// </summary>
	/// <param name="label">
	/// Optional descriptive label to include in the console output. Defaults to <c>"Performance"</c>.
	/// </param>
	/// <returns>
	/// A disposable <see cref="PerfScope"/> instance that automatically reports elapsed time and FPS when disposed.
	/// </returns>
	/// <example>
	/// <code>
	/// using var _ = PerfScope.Create("Frame Render");
	/// RenderFrame();
	/// </code>
	/// </example>
	public static PerfScope Create(string label = "Benchmark") => new(label);

	/// <summary>
	/// Executes a specified action multiple times and reports the average, best, and worst times.
	/// </summary>
	/// <param name="label">Descriptive label for this benchmark run.</param>
	/// <param name="iterations">Number of times the action will be executed.</param>
	/// <param name="action">The code block to measure.</param>
	public static void Run(string label, int iterations, Action action)
	{
		if (iterations <= 0)
			throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations must be greater than zero.");

		ArgumentNullException.ThrowIfNull(action);

		// Warm-up to reduce JIT influence
		action();

		double best = double.MaxValue;
		double worst = double.MinValue;
		double total = 0.0;
		var sw = new Stopwatch();

		for (int i = 0; i < iterations; i++)
		{
			sw.Restart();
			action();
			sw.Stop();

			double ms = sw.Elapsed.TotalMilliseconds;
			total += ms;
			if (ms < best) best = ms;
			if (ms > worst) worst = ms;
		}

		double avg = total / iterations;
		double fps = avg > 0 ? 1000.0 / avg : double.PositiveInfinity;

		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine();
		Console.WriteLine("═══════════════════════════════════════════════");
		Console.WriteLine($"{label} — {iterations} runs");
		Console.WriteLine("═══════════════════════════════════════════════");
		Console.ResetColor();

		Console.WriteLine($"Average : {avg:F3} ms");
		Console.WriteLine($"Best    : {best:F3} ms");
		Console.WriteLine($"Worst   : {worst:F3} ms");
		Console.WriteLine($"FPS est : {fps:F1} FPS");
		Console.WriteLine("═══════════════════════════════════════════════");
		Console.WriteLine();
	}

	/// <summary>
	/// Stops the performance timer and reports the elapsed time and calculated FPS to the console.
	/// </summary>
	/// <remarks>
	/// The console output follows the format:
	/// <c>{Label}: {Milliseconds} ms ({FPS} FPS)</c>
	/// </remarks>
	public void Dispose()
	{
		_watch!.Stop();
		double ms = _watch.Elapsed.TotalMilliseconds;
		double fps = ms > 0 ? 1000.0 / ms : double.PositiveInfinity;

		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.WriteLine();
		Console.WriteLine("───────────────────────────────────────────────");
		Console.WriteLine($"{(_label ?? "Performance Benchmark")}");
		Console.WriteLine("───────────────────────────────────────────────");
		Console.ResetColor();

		Console.WriteLine($"Elapsed Time : {ms:F2} ms");
		Console.WriteLine($"FPS Estimate  : {fps:F1} FPS");
		Console.WriteLine("───────────────────────────────────────────────");
		Console.WriteLine();
	}
}