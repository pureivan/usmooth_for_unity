﻿using UnityEngine;
using System.Collections;
using System;
using System.Text;
using System.Threading;
using LostPolygon.System.Net;
using LostPolygon.System.Net.Sockets;
using Random = System.Random;

using System.IO;
using usmooth.common;

public class UsNet : IDisposable {

	public static UsNet Instance;
	
	// TcpListener instance, encapsulating 
	// typical socket server interactions
	private TcpListener _tcpListener;

	private TcpClient _tcpClient;

	public UsCmdParsing CmdExecutor { get { return _cmdExec; } }
	private UsCmdParsing _cmdExec = new UsCmdParsing(); 
	
	// QOTD server constructor
	public UsNet(int port) {
		try {
			// Create a listening server that accepts connections from
			// any addresses on a given port
			_tcpListener = new TcpListener(IPAddress.Any, port);
			// Switch the listener to a started state
			_tcpListener.Start();
			// Set the callback that'll be called when a client connects to the server
			_tcpListener.BeginAcceptTcpClient(OnAcceptTcpClient, _tcpListener);
		} catch (Exception e) {
			AddToLog(e.ToString());
			throw;
		}
		
		AddToLog("usmooth listening started.");
	}
	
	~UsNet() {
		FreeResources();
	}
	
	// Free the resources
	public void Dispose() {
		
		CloseTcpClient ();
		
		if (_tcpListener != null) {
			FreeResources();
			
			AddToLog("Listening canceled.");
		}
	}
	
	private void FreeResources() {
		
		if (_tcpListener != null) {
			_tcpListener.Stop();
			_tcpListener = null;
		}
	}
	
	private void CloseTcpClient() {
		if (_tcpClient != null) {
			AddToLog(string.Format("Disconnecting client {0}.", _tcpClient.Client.RemoteEndPoint));
			_tcpClient.Close();
			_tcpClient = null;
		}
	}

	public void Update() {
		if (_tcpClient == null) {
			return;
		}
		
		try {
			while (_tcpClient.Available > 0) {
				byte[] cmdLenBuf = new byte[2];
				int cmdLenRead = _tcpClient.GetStream().Read(cmdLenBuf, 0, cmdLenBuf.Length);
				ushort cmdLen = BitConverter.ToUInt16(cmdLenBuf, 0);
				if (cmdLenRead > 0 && cmdLen > 0) {
					byte[] buffer = new byte[cmdLen];
					int len = _tcpClient.GetStream().Read(buffer, 0, buffer.Length);
					if (len == buffer.Length) {
						_cmdExec.Execute(new UsCmd(buffer));
					} else {
						AddToLog(string.Format("corrupted cmd received - len: {0}", len));
					}
				}
			}
		} catch (Exception ex) {
			Debug.LogException(ex);
			CloseTcpClient();
		}
	}

	public void SendCommand(UsCmd cmd) {
		if (_tcpClient == null || _tcpClient.GetStream() == null) {
			return;				
		}

		byte[] cmdLenBytes = BitConverter.GetBytes ((ushort)cmd.WrittenLen);
		_tcpClient.GetStream().Write(cmdLenBytes, 0, cmdLenBytes.Length);
		_tcpClient.GetStream().Write(cmd.Buffer, 0, cmd.WrittenLen);
		//Debug.Log (string.Format("cmd written, len ({0})", cmd.WrittenLen));
	}

	// Callback that gets called when a new incoming client
	// connection is established
	private void OnAcceptTcpClient(IAsyncResult asyncResult) {
		// Retrieve the TcpListener instance from IAsyncResult
		TcpListener listener = (TcpListener) asyncResult.AsyncState;
		if (listener == null)
			return;
		
		// Restart the connection accept procedure
		listener.BeginAcceptTcpClient(OnAcceptTcpClient, listener);
		
		try {
			// Retrieve newly connected TcpClient from IAsyncResult
			_tcpClient = listener.EndAcceptTcpClient(asyncResult);
			AddToLog(string.Format("Client {0} connected.", _tcpClient.Client.RemoteEndPoint));
		} catch (SocketException ex) {
			AddToLog(string.Format("<color=red>Error accepting TCP connection: {0}</color>", ex.Message));
		} catch (ObjectDisposedException) {
			// The listener was Stop()'d, disposing the underlying socket and
			// triggering the completion of the callback. We're already exiting,
			// so just ignore this.
		} catch (Exception ex) {
			// Some other error occured. This should not happen
			Debug.LogException(ex);
			AddToLog(string.Format("<color=red>An error occured: {0}</color>", ex.Message));
		}
	}

	// Adds a formatted entry to the log
	private void AddToLog(string text) {
		if (_tcpClient != null) {
			Debug.Log(string.Format("<color=green>{0}</color> <color=black>{1}</color>", _tcpClient.Client.RemoteEndPoint, text));
		} else {
			Debug.Log(string.Format("<color=black>{0}</color>", text));
		}
	}
}

