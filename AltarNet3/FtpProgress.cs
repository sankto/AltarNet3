using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace AltarNet {
	/// <summary>
	/// This class is used when downloading or uploading an FTP file, and must be inherited to be of use.
	/// </summary>
	public abstract class ProgressMonitorBase {
		private bool askSize = true;

		/// <summary>
		/// Get the minute details of the current progress of the download / upload.
		/// </summary>
		public FtpProgress Progress { get; internal set; }
		/// <summary>
		/// If set to true, this will ask for the size of the download / upload.
		/// </summary>
		public bool AskForSize { get { return askSize; } set { askSize = value; } }
		/// <summary>
		/// This will be called right after the size is found (or not) and the download / upload is ready to start.
		/// </summary>
		public abstract void OnInit();
		/// <summary>
		/// This will be called whenever this is progress on the download / upload.
		/// </summary>
		public abstract void Progressed();
	}

	/// <summary>
	/// Represent the minute details of the progress of a download or upload.
	/// </summary>
	public class FtpProgress {
		private ThreadSafeHelper<byte> RateSync;
		private Timer RateTimer;
		private long RateCount;

		/// <summary>
		/// Get the total length in bytes of the download / upload
		/// </summary>
		public readonly long TotalLength;
		/// <summary>
		/// Get the current amount of bytes that has been processed.
		/// </summary>
		public long CurrentCount { get; internal set; }
		/// <summary>
		/// When BytesPerSecondUpdated isn't null, get the transfert speed in bytes per second.
		/// </summary>
		public long BytesPerSecond { get; private set; }
		/// <summary>
		/// Get the completion state of the current progress.
		/// </summary>
		public bool IsCompleted { get { return CurrentCount == TotalLength; } }

		/// <summary>
		/// The event is called every second, updating the BytesPerSecond property with the current transfert rate.
		/// </summary>
		public event EventHandler BytesPerSecondUpdated;

		/// <summary>
		/// Create an FtpProgress, with the download / upload length in bytes.
		/// </summary>
		/// <param name="len">The total length in bytes of the download / upload</param>
		public FtpProgress(long len) {
			TotalLength = len;
			CurrentCount = 0L;
			RateCount = 0L;
			BytesPerSecond = 0L;
			RateSync = new ThreadSafeHelper<byte>();
			RateTimer = new Timer(1000);
			RateTimer.Elapsed += RateTimer_Elapsed;
		}

		void RateTimer_Elapsed(object sender, ElapsedEventArgs e) {
			try {
				RateSync.Wait(1);
				BytesPerSecond = RateCount;
				RateCount = 0;
			} finally {
				RateSync.Release(1);
			}
			if (BytesPerSecondUpdated != null)
				BytesPerSecondUpdated(this, EventArgs.Empty);
		}

		/// <summary>
		/// Return the percent done of the current download / upload.
		/// </summary>
		/// <returns>The percent, or -1 if TotalLength is -1.</returns>
		public int Percent() {
			if (TotalLength <= 0)
				return -1;
			return (int)(((float)CurrentCount / (float)TotalLength) * 100.0f);
		}

		internal void AddToRate(int count) {
			try {
				RateSync.Wait(1);
				RateCount += count;
			} finally {
				RateSync.Release(1);
			}
		}

		internal void StartRateTimer() {
			RateTimer.Start();
		}

		internal void StopRateTimer() {
			RateTimer.Stop();
			RateTimer.Dispose();
		}
	}
}
