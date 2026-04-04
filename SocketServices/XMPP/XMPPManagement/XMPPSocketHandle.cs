#pragma warning disable CA1031

namespace RadiantConnect.SocketServices.XMPP.XMPPManagement
{
	/// <summary>
	/// Represents a low-level socket handle for sending and receiving
	/// XMPP XML data over managed streams.
	/// </summary>
	/// <remarks>
	/// This class abstracts direct stream access and provides helper methods
	/// for safely writing XMPP XML stanzas to the appropriate stream.
	/// </remarks>
	public class XMPPSocketHandle(Stream? incomingStream, Stream outgoingStream, IEnumerable<Func<InterceptContext, Task>> clientIntercepts, IEnumerable<Func<InterceptContext, Task>> serverIntercepts) : IDisposable
	{
		private readonly CancellationTokenSource _cancellationTokenSource = new();
		internal bool DoBreak;
		internal delegate void InternalMessage(string data);

		internal event InternalMessage? OnClientMessage;
		internal event InternalMessage? OnServerMessage;
		
		/// <summary>
		/// Disposes the socket connection and disconnects from the MITM XMPP server.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Disposes the socket connection and disconnects from the MITM XMPP server.
		/// </summary>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;
			_cancellationTokenSource.Cancel();
			try { incomingStream?.Dispose(); } catch { /* ignored */ }
			try { outgoingStream.Dispose(); } catch { /* ignored */ }
			_cancellationTokenSource.Dispose();
		}

		internal void Initiate()
		{
			Task.Run(IncomingHandler);
			Task.Run(OutgoingHandler);
		}

		internal async Task IncomingHandler()
		{
			try
			{
				int byteCount;
				byte[] bytes = new byte[8192 * 2];
				do
				{
					if (incomingStream is null) break;
					byteCount = await incomingStream.ReadAsync(bytes, _cancellationTokenSource.Token).ConfigureAwait(false);
					string content = Encoding.UTF8.GetString(bytes, 0, byteCount);

					InterceptContext ctx = new()
					{
						Bytes = bytes,
						ByteCount = byteCount,
						Content = content
					};

					await RunIntercepts(clientIntercepts, ctx).ConfigureAwait(false);

					if (ctx.Action == InterceptAction.Forward)
					{
						await outgoingStream.WriteAsync(ctx.Bytes.AsMemory(0, ctx.ByteCount), _cancellationTokenSource.Token).ConfigureAwait(false);
						OnClientMessage?.Invoke(ctx.Content);
					}

					Array.Clear(bytes);
				} while (byteCount != 0 && !DoBreak);
			}
			catch (OperationCanceledException) { }
			catch (IOException) { }
			finally { DoBreak = true; }
		}

		internal async Task OutgoingHandler()
		{
			try
			{
				int byteCount;
				byte[] bytes = new byte[8192 * 2];
				do
				{
					if (incomingStream is null) break;
					byteCount = await outgoingStream.ReadAsync(bytes, _cancellationTokenSource.Token).ConfigureAwait(false);
					string content = Encoding.UTF8.GetString(bytes, 0, byteCount);

					InterceptContext ctx = new()
					{
						Bytes = bytes,
						ByteCount = byteCount,
						Content = content
					};

					await RunIntercepts(serverIntercepts, ctx).ConfigureAwait(false);

					if (ctx.Action == InterceptAction.Forward)
					{
						await incomingStream.WriteAsync(ctx.Bytes.AsMemory(0, ctx.ByteCount), _cancellationTokenSource.Token).ConfigureAwait(false);
						OnServerMessage?.Invoke(ctx.Content);
					}

					Array.Clear(bytes);
				} while (byteCount != 0 && !DoBreak);
			}
			catch (OperationCanceledException) { }
			catch (IOException) { }
			finally { DoBreak = true; }
		}

		private static async Task RunIntercepts(IEnumerable<Func<InterceptContext, Task>> intercepts, InterceptContext ctx)
		{
			foreach (Func<InterceptContext, Task> intercept in intercepts)
			{
				await intercept(ctx).ConfigureAwait(false);

				// Stop pipeline if any intercept blocks
				if (ctx.Action == InterceptAction.Block) break;
			}
		}

		/// <summary>
		/// Sends an XMPP XML message using the primary XML messaging pipeline.
		/// </summary>
		/// <param name="data">
		/// The XML payload to send.
		/// </param>
		/// <remarks>
		/// This method is typically used for internal or server-bound XMPP messages.
		/// </remarks>
		public async Task SendXmlMessageAsync([StringSyntax(StringSyntaxAttribute.Xml)] string data)
		{
			if (_cancellationTokenSource.IsCancellationRequested) return;

			try
			{
				while (!incomingStream?.CanWrite ?? false)
				{
					if (incomingStream is null) break;
					await Task.Delay(50).ConfigureAwait(false);
				}
				byte[] bytes = Encoding.UTF8.GetBytes(data);

				if (incomingStream is null) return;
				await incomingStream.WriteAsync(bytes.AsMemory(0, bytes.Length), _cancellationTokenSource.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) {/**/}
			catch (Exception ex) { Debug.WriteLine(ex); }
		}

		/// <summary>
		/// Sends an XMPP XML message directly to the outgoing stream.
		/// </summary>
		/// <param name="data">
		/// The raw XML payload to write to the outgoing stream.
		/// </param>
		/// <remarks>
		/// This method bypasses higher-level routing and writes directly
		/// to the underlying stream.
		/// </remarks>
		public async Task SendXmlToOutgoingStream([StringSyntax(StringSyntaxAttribute.Xml)] string data)
		{
			try
			{
				while (!outgoingStream.CanWrite) await Task.Delay(50).ConfigureAwait(false);
				byte[] bytes = Encoding.UTF8.GetBytes(data);
				await outgoingStream.WriteAsync(bytes.AsMemory(0, bytes.Length), _cancellationTokenSource.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) {/**/}
			catch (Exception ex) { Debug.WriteLine(ex); }
		}
	}
}
