using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AltarNet {
	/// <summary>
	/// Describe a path to an FTP element, weither it be a directory or a file. Actions can be taken on it's target.
	/// </summary>
	public class FtpRemoteItem : ICloneable {
		private enum UploadMode { 
			Upload,
			UploadAsUnique,
			Append
		}

		/// <summary>
		/// Get the FtpHandler that initialy created the item.
		/// </summary>
		public readonly FtpHandler Handler;
		/// <summary>
		/// Get the parent Item which has selected this item.
		/// </summary>
		public readonly FtpRemoteItem Parent;
		/// <summary>
		/// Get the path to the targetted element.
		/// </summary>
		public string Target { get; private set; }

		internal FtpRemoteItem(FtpHandler handler, FtpRemoteItem parent, string path) {
			Handler = handler;
			Parent = parent;
			Target = path;
		}

		/// <summary>
		/// Do a shallow copy of this item.
		/// </summary>
		/// <returns>The copy.</returns>
		public FtpRemoteItem Clone() {
			return this.MemberwiseClone() as FtpRemoteItem;
		}

		/// <summary>
		/// Do a shallow copy of this item.
		/// </summary>
		/// <returns>The copy.</returns>
		object ICloneable.Clone() {
			return Clone();
		}

		/// <summary>
		/// Create and return a subitem with the given path, with a Target value which ends up being 'Target + "/" + remotePath'.
		/// </summary>
		/// <param name="remotePath">The target for the new subitem</param>
		/// <returns>The subitem</returns>
		public FtpRemoteItem Select(string remotePath = null) {
			return new FtpRemoteItem(Handler, this, Target + "/" + (remotePath ?? string.Empty));
		}

		/// <summary>
		/// Query is the named item exists in the targeted directory, weither the named item be a directory or a file.
		/// </summary>
		/// <param name="name">The file/directory to look for</param>
		/// <param name="caseSensitive">Weither case sensitivity should be observed or not</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>True if the item is found, false otherwise</returns>
		public async Task<bool> Exists(string name, bool caseSensitive = false, FtpOptions options = null) {
			return await Exists(name, CancellationToken.None, caseSensitive);
		}
		/// <summary>
		/// Query is the named item exists in the targeted directory, weither the named item be a directory or a file.
		/// </summary>
		/// <param name="name">The file/directory to look for</param>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="caseSensitive">Weither case sensitivity should be observed or not</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>True if the item is found, false otherwise</returns>
		public async Task<bool> Exists(string name, CancellationToken cancelToken, bool caseSensitive = false, FtpOptions options = null) {
			if (caseSensitive == false)
				name = name.ToLower();
			return (await List(cancelToken)).Any((path) => 
				(caseSensitive ? Path.GetFileName(path) : Path.GetFileName(path).ToLower()) == name);
		}

		/// <summary>
		/// Query the list of files and directories in the targeted directory.
		/// </summary>
		/// <param name="options">The query-specific options</param>
		/// <returns>A list of all the files and directories</returns>
		public async Task<List<string>> List(FtpOptions options = null) {
			return await List(CancellationToken.None, options);
		}
		/// <summary>
		/// Query the list of files and directories in the targeted directory.
		/// </summary>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A list of all the files and directories</returns>
		public async Task<List<string>> List(CancellationToken cancelToken, FtpOptions options = null) {
			cancelToken.ThrowIfCancellationRequested();
			FtpWebRequest request = Handler.MakeRequest(cancelToken, options, Target, WebRequestMethods.Ftp.ListDirectory);
			using (var response = (FtpWebResponse)await request.GetResponseAsync()) {
				cancelToken.ThrowIfCancellationRequested();
				using (var reader = new StreamReader(response.GetResponseStream())) {
					var list = new List<string>();
					while (reader.Peek() != -1) {
						list.Add(await reader.ReadLineAsync());
						cancelToken.ThrowIfCancellationRequested();
					} return list;
				}
			}
		}

		/// <summary>
		/// Query the details of a list of files and directories in the targeted directory.
		/// </summary>
		/// <param name="options">The query-specific options</param>
		/// <returns>A detailed list of all the files and directories</returns>
		public async Task<List<string>> ListDetails(FtpOptions options = null) {
			return await ListDetails(CancellationToken.None, options);
		}
		/// <summary>
		/// Query the details of a list of files and directories in the targeted directory.
		/// </summary>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A detailed list of all the files and directories</returns>
		public async Task<List<string>> ListDetails(CancellationToken cancelToken, FtpOptions options = null) {
			cancelToken.ThrowIfCancellationRequested();
			FtpWebRequest request = Handler.MakeRequest(cancelToken, options, Target, WebRequestMethods.Ftp.ListDirectoryDetails);
			using (var response = (FtpWebResponse)await request.GetResponseAsync()) {
				cancelToken.ThrowIfCancellationRequested();
				using (var reader = new StreamReader(response.GetResponseStream())) {
					var list = new List<string>();
					while (reader.Peek() != -1) {
						list.Add(await reader.ReadLineAsync());
						cancelToken.ThrowIfCancellationRequested();
					} return list;
				}
			}
		}

		/// <summary>
		/// Request a Rename action on the targeted file or directory.
		/// </summary>
		/// <param name="newName">The new name for the file or directory</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Rename(string newName, FtpOptions options = null) {
			await Rename(newName, CancellationToken.None, options);
		}
		/// <summary>
		/// Request a Rename action on the targeted file or directory.
		/// </summary>
		/// <param name="newName">The new name for the file or directory</param>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Rename(string newName, CancellationToken cancelToken, FtpOptions options = null) {
			cancelToken.ThrowIfCancellationRequested();
			FtpWebRequest request = Handler.MakeRequest(cancelToken, options, Target, WebRequestMethods.Ftp.Rename);
			request.RenameTo = newName;
			using (var response = (FtpWebResponse)await request.GetResponseAsync()) {
				cancelToken.ThrowIfCancellationRequested();
				if (response.StatusCode == FtpStatusCode.CommandOK)
					Target = Path.Combine(Path.GetDirectoryName(Target), newName);
			}
		}

		/// <summary>
		/// Request a Delete action on the targeted file.
		/// </summary>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task DeleteFile(FtpOptions options = null) {
			await DeleteFile(CancellationToken.None, options);
		}
		/// <summary>
		/// Request a Delete action on the targeted file.
		/// </summary>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task DeleteFile(CancellationToken cancelToken, FtpOptions options = null) {
			cancelToken.ThrowIfCancellationRequested();
			FtpWebRequest request = Handler.MakeRequest(cancelToken, options, Target, WebRequestMethods.Ftp.DeleteFile);
			using (var response = (FtpWebResponse)await request.GetResponseAsync())
				cancelToken.ThrowIfCancellationRequested();
		}

		/// <summary>
		/// Request a MakeDirectory action on the targeted Directory. The target must not exists.
		/// </summary>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task MakeDirectory(FtpOptions options = null) {
			await MakeDirectory(CancellationToken.None, options);
		}
		/// <summary>
		/// Request a MakeDirectory action on the targeted Directory. The target must not exists.
		/// </summary>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task MakeDirectory(CancellationToken cancelToken, FtpOptions options = null) {
			cancelToken.ThrowIfCancellationRequested();
			FtpWebRequest request = Handler.MakeRequest(cancelToken, options, Target, WebRequestMethods.Ftp.MakeDirectory);
			using (var response = (FtpWebResponse)await request.GetResponseAsync())
				cancelToken.ThrowIfCancellationRequested();
		}

		/// <summary>
		/// Request a RemoveDirectory action on the targeted Directory. The target must exists.
		/// </summary>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task RemoveDirectory(FtpOptions options = null) {
			await RemoveDirectory(CancellationToken.None, options);
		}
		/// <summary>
		/// Request a RemoveDirectory action on the targeted Directory. The target must exists.
		/// </summary>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task RemoveDirectory(CancellationToken cancelToken, FtpOptions options = null) {
			cancelToken.ThrowIfCancellationRequested();
			FtpWebRequest request = Handler.MakeRequest(cancelToken, options, Target, WebRequestMethods.Ftp.RemoveDirectory);
			using (var response = (FtpWebResponse)await request.GetResponseAsync())
				cancelToken.ThrowIfCancellationRequested();
		}

		/// <summary>
		/// Query the Current directory.
		/// </summary>
		/// <param name="options">The query-specific options</param>
		/// <returns>The working directory</returns>
		public async Task<string> PrintWorkingDirectory(FtpOptions options = null) {
			return await PrintWorkingDirectory(CancellationToken.None, options);
		}
		/// <summary>
		/// Query the Current directory.
		/// </summary>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>The working directory</returns>
		public async Task<string> PrintWorkingDirectory(CancellationToken cancelToken, FtpOptions options = null) {
			cancelToken.ThrowIfCancellationRequested();
			FtpWebRequest request = Handler.MakeRequest(cancelToken, options, Target, WebRequestMethods.Ftp.PrintWorkingDirectory);
			using (var response = (FtpWebResponse)await request.GetResponseAsync()) {
				cancelToken.ThrowIfCancellationRequested();
				return response.StatusDescription;
			}
		}

		/// <summary>
		/// Query the file size of the target.
		/// </summary>
		/// <param name="options">The query-specific options</param>
		/// <returns>The size of the targeted file</returns>
		public async Task<long> GetFileSize(FtpOptions options = null) {
			return await GetFileSize(CancellationToken.None, options);
		}
		/// <summary>
		/// Query the file size of the target.
		/// </summary>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>The size of the targeted file</returns>
		public async Task<long> GetFileSize(CancellationToken cancelToken, FtpOptions options = null) {
			cancelToken.ThrowIfCancellationRequested();
			FtpWebRequest request = Handler.MakeRequest(cancelToken, options, Target, WebRequestMethods.Ftp.GetFileSize);
			using (var response = (FtpWebResponse)await request.GetResponseAsync()) {
				cancelToken.ThrowIfCancellationRequested();
				using (var sizeStream = response.GetResponseStream())
					return response.ContentLength;
			}
		}

		/// <summary>
		/// Query the last modified time of the target.
		/// </summary>
		/// <param name="options">The query-specific options</param>
		/// <returns>The last modified time of the targeted file</returns>
		public async Task<DateTime> GetDatestamp(FtpOptions options = null) {
			return await GetDatestamp(CancellationToken.None, options);
		}
		/// <summary>
		/// Query the last modified time of the target.
		/// </summary>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>The last modified time of the targeted file</returns>
		public async Task<DateTime> GetDatestamp(CancellationToken cancelToken, FtpOptions options = null) {
			cancelToken.ThrowIfCancellationRequested();
			FtpWebRequest request = Handler.MakeRequest(cancelToken, options, Target, WebRequestMethods.Ftp.GetDateTimestamp);
			using (var response = (FtpWebResponse)await request.GetResponseAsync()) {
				cancelToken.ThrowIfCancellationRequested();
				return response.LastModified;
			}	
		}

		/// <summary>
		/// Request a Download action on the targeted file, returning an array of bytes.
		/// </summary>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>The full array of bytes of the targeted file</returns>
		public async Task<byte[]> DownloadData(ProgressMonitorBase progress = null, FtpOptions options = null) {
			return await DownloadData(CancellationToken.None, progress, options);
		}
		/// <summary>
		/// Request a Download action on the targeted file, returning an array of bytes.
		/// </summary>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>The full array of bytes of the targeted file</returns>
		public async Task<byte[]> DownloadData(CancellationToken cancelToken, ProgressMonitorBase progress = null, FtpOptions options = null) {
			using (var mem = new MemoryStream()) {
				await Download(mem, cancelToken, progress, options);
				return mem.ToArray();
			}
		}
		/// <summary>
		/// Request a Download action on the targeted file, returning a string.
		/// </summary>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>The full string of the targeted file</returns>
		public async Task<string> DownloadString(ProgressMonitorBase progress = null, FtpOptions options = null) {
			return await DownloadString(CancellationToken.None, progress, options);
		}
		/// <summary>
		/// Request a Download action on the targeted file, returning a string.
		/// </summary>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>The full string of the targeted file</returns>
		public async Task<string> DownloadString(CancellationToken cancelToken, ProgressMonitorBase progress = null, FtpOptions options = null) {
			using (var mem = new MemoryStream()) {
				await Download(mem, cancelToken, progress, options);
				using (var reader = new StreamReader(mem))
					return await reader.ReadToEndAsync();
			}
		}
		/// <summary>
		/// Request a Download action on the targeted file, returning a string.
		/// </summary>
		/// <param name="encode">The encoding to use to reclaim the data into a string</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>The full string of the targeted file</returns>
		public async Task<string> DownloadString(Encoding encode, ProgressMonitorBase progress = null, FtpOptions options = null) {
			return await DownloadString(encode, CancellationToken.None, progress, options);
		}
		/// <summary>
		/// Request a Download action on the targeted file, returning a string.
		/// </summary>
		/// <param name="encode">The encoding to use to reclaim the data into a string</param>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>The full string of the targeted file</returns>
		public async Task<string> DownloadString(Encoding encode, CancellationToken cancelToken, ProgressMonitorBase progress = null, FtpOptions options = null) {
			using (var mem = new MemoryStream()) {
				await Download(mem, cancelToken, progress, options);
				using (var reader = new StreamReader(mem, encode))
					return await reader.ReadToEndAsync();
			}
		}
		/// <summary>
		/// Request a Download action on the targeted file.
		/// </summary>
		/// <param name="filename">The local file that will be written to</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Download(string filename, ProgressMonitorBase progress = null, FtpOptions options = null) {
			using (var filestream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
				await Download(filestream, CancellationToken.None, progress, options);
		}
		/// <summary>
		/// Request a Download action on the targeted file.
		/// </summary>
		/// <param name="filename">The local file that will be written to</param>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Download(string filename, CancellationToken cancelToken, ProgressMonitorBase progress = null, FtpOptions options = null) {
			using (var filestream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
				await Download(filestream, cancelToken, progress, options);
		}
		/// <summary>
		/// Request a Download action on the targeted file.
		/// </summary>
		/// <param name="stream">The stream that will be written to</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Download(Stream stream, ProgressMonitorBase progress = null, FtpOptions options = null) {
			await Download(stream, CancellationToken.None, progress, options);
		}
		/// <summary>
		/// Request a Download action on the targeted file.
		/// </summary>
		/// <param name="stream">The stream that will be written to</param>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Download(Stream stream, CancellationToken cancelToken, ProgressMonitorBase progress = null, FtpOptions options = null) {
			cancelToken.ThrowIfCancellationRequested();
			options = options ?? Handler.Options;
			FtpWebResponse response = null;
			FtpWebRequest request = null;
			if (progress != null) {
				long size = -1;
				if (progress.AskForSize)
					try {
						size = await GetFileSize(cancelToken);
					} catch { }
				progress.Progress = new FtpProgress(size);
				progress.OnInit();
			}
			if (cancelToken.IsCancellationRequested)
				cancelToken.ThrowIfCancellationRequested();
			request = Handler.MakeRequest(cancelToken, options, Target, WebRequestMethods.Ftp.DownloadFile);
			response = (FtpWebResponse)await request.GetResponseAsync();
			cancelToken.ThrowIfCancellationRequested();
			byte[] buffer = new byte[options.BufferSize];
			int readCount = 0;
			try {
				using (var responseStream = response.GetResponseStream()) {
					if (progress != null)
						progress.Progress.StartRateTimer();
					do {
						readCount = await responseStream.ReadAsync(buffer, 0, options.BufferSize, cancelToken);
						await stream.WriteAsync(buffer, 0, readCount, cancelToken);
						cancelToken.ThrowIfCancellationRequested();
						if (progress != null) {
							progress.Progress.CurrentCount += readCount;
							progress.Progress.AddToRate(readCount);
							progress.Progressed();
						}
					} while (readCount > 0);
				}
			} finally {
				if (progress != null)
					progress.Progress.StopRateTimer();
			}
		}

		/// <summary>
		/// Request a Upload action on the targeted file.
		/// </summary>
		/// <param name="filename">The local file that will be read from</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Upload(string filename, ProgressMonitorBase progress = null, FtpOptions options = null) {
			using (var filestream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
				await InternalUpload(UploadMode.Upload, filestream, CancellationToken.None, progress, options);
		}
		/// <summary>
		/// Request a Upload action on the targeted file.
		/// </summary>
		/// <param name="filename">The local file that will be read from</param>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Upload(string filename, CancellationToken cancelToken, ProgressMonitorBase progress = null, FtpOptions options = null) {
			using (var filestream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
				await InternalUpload(UploadMode.Upload, filestream, cancelToken, progress, options);
		}
		/// <summary>
		/// Request a Upload action on the targeted file.
		/// </summary>
		/// <param name="stream">The stream that will be read from</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Upload(Stream stream, ProgressMonitorBase progress = null, FtpOptions options = null) {
			await InternalUpload(UploadMode.Upload, stream, CancellationToken.None, progress, options);
		}
		/// <summary>
		/// Request a Upload action on the targeted file.
		/// </summary>
		/// <param name="stream">The stream that will be read from</param>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Upload(Stream stream, CancellationToken cancelToken, ProgressMonitorBase progress = null, FtpOptions options = null) {
			await InternalUpload(UploadMode.Upload, stream, cancelToken, progress, options);
		}
		private async Task<string> InternalUpload(UploadMode mode, Stream stream, CancellationToken cancelToken, ProgressMonitorBase progress, FtpOptions options = null) {
			cancelToken.ThrowIfCancellationRequested();
			options = options ?? Handler.Options;
			long size = -1;
			if (progress != null) {
				if (progress.AskForSize)
					try {
						size = stream.Length;
					} catch (NotSupportedException) { }
				progress.Progress = new FtpProgress(size);
				progress.OnInit();
			}
			byte[] buffer = new byte[options.BufferSize];
			int readCount = 0;
			var protocol = string.Empty;
			switch (mode) {
				case UploadMode.Upload:
					protocol = WebRequestMethods.Ftp.UploadFile;
					break;
				case UploadMode.UploadAsUnique:
					protocol = WebRequestMethods.Ftp.UploadFileWithUniqueName;
					break;
				case UploadMode.Append:
					protocol = WebRequestMethods.Ftp.AppendFile;
					break;
			};
			FtpWebRequest request = Handler.MakeRequest(cancelToken, options, Target, protocol);
			if (size != -1)
				request.ContentLength = size;
			using (var requestStream = await request.GetRequestStreamAsync()) {
				cancelToken.ThrowIfCancellationRequested();
				do {
					readCount = await stream.ReadAsync(buffer, 0, options.BufferSize, cancelToken);
					await requestStream.WriteAsync(buffer, 0, readCount, cancelToken);
					cancelToken.ThrowIfCancellationRequested();
					if (progress != null) {
						progress.Progress.CurrentCount += readCount;
						progress.Progressed();
					}
				} while (readCount > 0);
			}
			if (mode == UploadMode.UploadAsUnique)
				using (var response = (FtpWebResponse)await request.GetResponseAsync()) {
					cancelToken.ThrowIfCancellationRequested();
					return Path.GetFileName(response.ResponseUri.ToString());
				}
			return null;
		}

		/// <summary>
		/// Request a Upload action on the targeted directory, with a unique, randomly generated name.
		/// </summary>
		/// <param name="filename">The local file that will be read from</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>The filename of the generated file</returns>
		public async Task<string> UploadWithUniqueName(string filename, ProgressMonitorBase progress = null, FtpOptions options = null) {
			using (var filestream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
				return await InternalUpload(UploadMode.UploadAsUnique, filestream, CancellationToken.None, progress, options);
		}
		/// <summary>
		/// Request a Upload action on the targeted directory, with a unique, randomly generated name.
		/// </summary>
		/// <param name="filename">The local file that will be read from</param>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>The filename of the generated file</returns>
		public async Task<string> UploadWithUniqueName(string filename, CancellationToken cancelToken, ProgressMonitorBase progress = null, FtpOptions options = null) {
			using (var filestream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
				return await InternalUpload(UploadMode.UploadAsUnique, filestream, cancelToken, progress, options);
		}
		/// <summary>
		/// Request a Upload action on the targeted directory, with a unique, randomly generated name.
		/// </summary>
		/// <param name="stream">The stream that will be read from</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>The filename of the generated file</returns>
		public async Task<string> UploadWithUniqueName(Stream stream, ProgressMonitorBase progress = null, FtpOptions options = null) {
			return await InternalUpload(UploadMode.UploadAsUnique, stream, CancellationToken.None, progress, options);
		}
		/// <summary>
		/// Request a Upload action on the targeted directory, with a unique, randomly generated name.
		/// </summary>
		/// <param name="stream">The stream that will be read from</param>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>The filename of the generated file</returns>
		public async Task<string> UploadWithUniqueName(Stream stream, CancellationToken cancelToken, ProgressMonitorBase progress = null, FtpOptions options = null) {
			return await InternalUpload(UploadMode.UploadAsUnique, stream, cancelToken, progress, options);
		}

		/// <summary>
		/// Request an Append action on the targeted file.
		/// </summary>
		/// <param name="filename">The local file that will be read from</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Append(string filename, ProgressMonitorBase progress = null, FtpOptions options = null) {
			using (var filestream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
				await InternalUpload(UploadMode.Append, filestream, CancellationToken.None, progress, options);
		}
		/// <summary>
		/// Request an Append action on the targeted file.
		/// </summary>
		/// <param name="filename">The local file that will be read from</param>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Append(string filename, CancellationToken cancelToken, ProgressMonitorBase progress = null, FtpOptions options = null) {
			using (var filestream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
				await InternalUpload(UploadMode.Append, filestream, cancelToken, progress, options);
		}
		/// <summary>
		/// Request an Append action on the targeted file.
		/// </summary>
		/// <param name="stream">The stream that will be read from</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Append(Stream stream, ProgressMonitorBase progress = null, FtpOptions options = null) {
			await InternalUpload(UploadMode.Append, stream, CancellationToken.None, progress, options);
		}
		/// <summary>
		/// Request an Append action on the targeted file.
		/// </summary>
		/// <param name="stream">The stream that will be read from</param>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="progress">The progress monitor</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>A Task</returns>
		public async Task Append(Stream stream, CancellationToken cancelToken, ProgressMonitorBase progress = null, FtpOptions options = null) {
			await InternalUpload(UploadMode.Append, stream, cancelToken, progress, options);
		}
	}
}
