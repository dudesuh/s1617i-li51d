/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Fila copy using the .NET Asynchronous Programming Model (APM)
 *
 * To generate the executable (using the library generic-async-result.dll) execute the following command:
 *	csc /reference:generic-async-result.dll /out:file-copy.exe file-copy.cs
 * or, just
 *	nmake
 * to clean all generated files, execute:
 *	nmake clean
 *	
 * Carlos Martins, January 2017
 *
 **/

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

public class FileCopyApm {

	const int BUFFER_SIZE = 32 * 1024;

	//
	// File copy using synchronous read and write operations.
	//
	
	public static IAsyncResult BeginCopySync(Stream src, Stream dst,
		 							AsyncCallback cb, object state) {		
		byte[] buffer = new byte[BUFFER_SIZE];
		long fileSize = 0;
		try {
			int bytesRead;
			while ((bytesRead = src.Read(buffer, 0, buffer.Length)) != 0) {
				dst.Write(buffer, 0, bytesRead);
				fileSize += bytesRead;
			}
		} finally {
			src.Close();
			dst.Close();
		}
		return GenericAsyncResult<long>.FromResult(cb, state, fileSize, null, true);
	}

	//
	// Returns the result from an asynchronous copy operation
	//
	
	public static long EndCopy(IAsyncResult ar) {
		return ((GenericAsyncResult<long>)ar).Result;
	}		

	//
	// File copy using APM asynchronous read and synchronous write operations.
	//
	
	public static IAsyncResult BeginCopyAsync(Stream src, Stream dst, AsyncCallback cb, object state) {
		GenericAsyncResult<long> gar = new GenericAsyncResult<long>(cb, state, false);
		byte[] rdBuffer = new byte[BUFFER_SIZE], wrBuffer = new byte[BUFFER_SIZE];
		long fileSize = 0;
		AsyncCallback onReadCompleted = null;		
		onReadCompleted = delegate(IAsyncResult ar) {
			int bytesRead = 0;
			try {
				bytesRead = src.EndRead(ar);
			} catch (IOException ioex) {
				src.Close();
				gar.OnComplete(0, ioex);
				return;
			}
			if (bytesRead > 0) {

				//
				// Switch the buffers.
				//
				// The lock ensures that we can't process the completion of the
				// new read (writing the underlying buffer buffer) before write
				// the current read buffer.
				//
				byte[] tmp = rdBuffer;
				rdBuffer = wrBuffer;
				wrBuffer = tmp;
				lock(dst) {
					src.BeginRead(rdBuffer, 0, rdBuffer.Length, onReadCompleted, null);
					dst.Write(wrBuffer, 0, bytesRead);
					fileSize += bytesRead;
				}
			} else {
			
				//
				// We reach the EOF of the source stream.
				// We must ensure that the write of the last block is done,
				// before close the destination stream and complete the
				// underlying task.
				//

				src.Close();
				lock(dst) {
					gar.OnComplete(fileSize, null);
				}
				dst.Close();
				return;
			}
		};
		
		//
		// Start the copy process starting the first asynchronous read.
		//
		
		src.BeginRead(rdBuffer, 0, rdBuffer.Length, onReadCompleted, null);
		return gar;
	}
	 
	//
	// File copy using APM performs asynchronous reads and asynchronous write operations.
	//

	public static IAsyncResult BeginCopyAsync2(Stream src, Stream dst,
		 									AsyncCallback cb, object state) {
		GenericAsyncResult<long> gar = new GenericAsyncResult<long>(cb, state, false);		
		long fileSize = 0;
		int pendingWrites = 1;		// Account for the last read that reads 0 bytes.
		AsyncCallback onReadCompleted = null, onWriteCompleted = null;		
		onReadCompleted = delegate (IAsyncResult ar) {
			int bytesRead = 0;
			try {
				bytesRead = src.EndRead(ar);
			} catch (IOException ioex) {
				src.Close();
				gar.OnComplete(0, ioex);
				return;
			}
			byte[] readBuf = (byte[])ar.AsyncState;
			if (bytesRead > 0) {

				//
				// Start an asynchronous write, and then the next asynchronous read.
				//
				
				lock(dst) {
					pendingWrites++;
					dst.BeginWrite(readBuf, 0, bytesRead, onWriteCompleted, bytesRead);
				}
				
				//
				// Allocate a new buffer and start a new asynchronous read.
				//
				
				byte[] nextBuf = new byte[BUFFER_SIZE];
				src.BeginRead(nextBuf, 0, nextBuf.Length, onReadCompleted, nextBuf);
			} else {
			
				// End of source file.
				
				src.Close();
				lock(dst) {
					if (--pendingWrites == 0) {
						
						//
						// Last read is a zero-length read no writes are in progress.
						// The copy is completed.
						//
						
						dst.Close();
						gar.OnComplete(fileSize, null);
					}
				}
			}
		};
		
		onWriteCompleted = delegate (IAsyncResult ar) {
			try {
				dst.EndWrite(ar);
			} catch (IOException ioex) {
				dst.Close();				
				gar.OnComplete(0, ioex);
				return;
			}
			lock(dst) {
				fileSize += (int)ar.AsyncState;
				if (--pendingWrites == 0) {
					dst.Close();
					gar.OnComplete(fileSize, null);
				}
			}
		};
		byte[] firstBuf = new byte[BUFFER_SIZE];
		src.BeginRead(firstBuf, 0, firstBuf.Length, onReadCompleted, firstBuf);
		return gar;
	}
	
	static void Main() {
		const bool AsyncMode = true;
		int iocp, worker;
		
		ThreadPool.GetMinThreads(out worker, out iocp);
		ThreadPool.SetMinThreads(worker, 2 * iocp);
		

		FileStream src = new FileStream(@"c:\windows\system32\ntoskrnl.exe",
										FileMode.Open, FileAccess.Read,
										FileShare.None, BUFFER_SIZE, AsyncMode);		
		FileStream dst = new FileStream(@".\result.dat",
										FileMode.Create, FileAccess.Write,
										FileShare.None, BUFFER_SIZE, AsyncMode);
										
		Stopwatch sw = Stopwatch.StartNew();
		IAsyncResult copyAsyncResult;
//		copyAsyncResult = BeginCopySync(src, dst, null, null);
//		copyAsyncResult = BeginCopyAsync(src, dst, null, null);
		copyAsyncResult = BeginCopyAsync2(src, dst, null, null);
		long elapsedUntilReturn = sw.ElapsedMilliseconds;
		// wait until copy terminates
		long bytesWriten = EndCopy(copyAsyncResult);
		sw.Stop();
		long elapsedUntilEnd = sw.ElapsedMilliseconds;
		Console.WriteLine("--elapsed until return: {0} ms; up to {1} bytes are copied: {2} ms",
			 	 		  elapsedUntilReturn, bytesWriten, elapsedUntilEnd);
	}
}

