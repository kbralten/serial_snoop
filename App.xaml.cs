using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SerialSnoop.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		DispatcherUnhandledException += App_DispatcherUnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
	}

	private static void App_DispatcherUnhandledException(object? sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
	{
		TryLogException("DispatcherUnhandledException", e.Exception);
		// let normal WPF handling continue (avoid swallowing exceptions silently)
	}

	private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		TryLogException("CurrentDomain_UnhandledException", e.ExceptionObject as Exception);
	}

	private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		TryLogException("UnobservedTaskException", e.Exception);
		// mark as observed so it doesn't terminate process on older runtimes
		e.SetObserved();
	}

	private static void TryLogException(string source, Exception? ex)
	{
		try
		{
			Directory.CreateDirectory("logs");
			var path = Path.Combine("logs", "crash.log");
			var sb = new StringBuilder();
			sb.AppendLine($"[{DateTime.Now:o}] {source}");
			if (ex is null)
			{
				sb.AppendLine("(no exception object)");
			}
			else
			{
				sb.AppendLine(ex.ToString());
			}
			sb.AppendLine(new string('-', 80));
			File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
		}
		catch
		{
			// best-effort only
		}
	}
}

