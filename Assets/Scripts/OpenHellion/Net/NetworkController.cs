using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using Steamworks;
using System;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using OpenHellion.IO;
using OpenHellion.Social.RichPresence;
using ZeroGravity.Network;

namespace OpenHellion.Net
{
	public class NetworkController : MonoBehaviour
	{
		private static GsConnection _gameConnection;

		private bool _getP2PPacketsThreadActive;

		private readonly HashSet<long> _spawnObjectsList = new HashSet<long>();

		private readonly HashSet<long> _subscribeToObjectsList = new HashSet<long>();

		private readonly HashSet<long> _unsubscribeFromObjectsList = new HashSet<long>();

		private readonly ConcurrentQueue<Tuple<float, Type>> _sentLog = new ConcurrentQueue<Tuple<float, Type>>();

		private readonly ConcurrentQueue<Tuple<float, Type>> _receivedLog = new ConcurrentQueue<Tuple<float, Type>>();

		private const int MaxNetworkDataLogsSize = 3000;

		private readonly DateTime _clientStartTime = DateTime.UtcNow.ToUniversalTime();

		private static NetworkController _instance;

		public static NetworkController Instance
		{
			get
			{
				if (_instance is null)
				{
					Debug.LogError("Tried to get network controller before it has been initialised.");
				}

				return _instance;
			}
		}

		private void Awake()
		{
			// Only one instance allowed.
			if (_instance != null)
			{
				Destroy(this);
				return;
			}

			_instance = this;
		}

		private void FixedUpdate()
		{
			EventSystem.InvokeQueuedData();

			if (_spawnObjectsList.Count > 0)
			{
				SpawnObjectsRequest spawnObjectsRequest = new SpawnObjectsRequest
				{
					GUIDs = new List<long>(_spawnObjectsList)
				};

				SendToGameServer(spawnObjectsRequest);
				_spawnObjectsList.Clear();
			}

			if (_subscribeToObjectsList.Count > 0)
			{
				SubscribeToObjectsRequest subscribeToObjectsRequest = new SubscribeToObjectsRequest
				{
					GUIDs = new List<long>(_subscribeToObjectsList)
				};

				SendToGameServer(subscribeToObjectsRequest);
				_subscribeToObjectsList.Clear();
			}

			if (_unsubscribeFromObjectsList.Count > 0)
			{
				UnsubscribeFromObjectsRequest unsubscribeFromObjectsRequest = new UnsubscribeFromObjectsRequest
				{
					GUIDs = new List<long>(_unsubscribeFromObjectsList)
				};

				SendToGameServer(unsubscribeFromObjectsRequest);
				_unsubscribeFromObjectsList.Clear();
			}

			_gameConnection?.Tick();

			// Handle Steam P2P packets.
			if (RichPresenceManager.HasSteam && !_getP2PPacketsThreadActive)
			{
				new Thread(P2PPacketListener).Start();
			}
		}

		public void RequestObjectSpawn(long guid)
		{
			_spawnObjectsList.Add(guid);
		}

		public void RequestObjectSubscribe(long guid)
		{
			_subscribeToObjectsList.Add(guid);
			_unsubscribeFromObjectsList.Remove(guid);
		}

		public void RequestObjectUnsubscribe(long guid)
		{
			_unsubscribeFromObjectsList.Add(guid);
			_subscribeToObjectsList.Remove(guid);
		}

		public static void ConnectToGame(ServerData serverData, Action onConnected, Action onDisconnected)
		{
			_gameConnection?.Disconnect();
			_gameConnection = new GsConnection();

			_gameConnection.Connect(serverData.IpAddress, serverData.GamePort, onConnected, onDisconnected);
		}

		public static void SendToGameServer(NetworkData data)
		{
			_gameConnection.Send(data);
		}

		/// <summary>
		/// 	Checks the latency between the client and server.
		/// </summary>
		public static async Task<int> LatencyTest(string address, int port, bool logException = false)
		{
			try
			{
				TcpClient tcpClient = new TcpClient(address, port);

				NetworkStream networkStream = tcpClient.GetStream();
				networkStream.ReadTimeout = 1000;
				networkStream.WriteTimeout = 1000;

				byte[] rawData = await ProtoSerialiser.Pack(new LatencyTestMessage());
				DateTime dateTime = DateTime.UtcNow.ToUniversalTime();

				// Send data.
				await networkStream.WriteAsync(rawData, 0, rawData.Length);
				await networkStream.FlushAsync();

				return (int)(DateTime.UtcNow - dateTime).TotalMilliseconds;
			}
			catch (Exception ex)
			{
				if (logException)
				{
					Debug.LogException(ex);
				}

				return -1;
			}
		}

		/// <summary>
		/// 	Send a request directly to a TCP endpoint.<br />
		/// 	Useful for status requests.
		/// </summary>
		public static async Task<NetworkData> SendTcp(NetworkData data, string address, int port,
			bool getResponse = true, bool logException = false)
		{
			try
			{
				TcpClient tcpClient = new TcpClient(address, port);

				NetworkStream networkStream = tcpClient.GetStream();
				networkStream.ReadTimeout = 1000;
				networkStream.WriteTimeout = 1000;

				byte[] rawData = await ProtoSerialiser.Pack(data);

				// Send data.
				await networkStream.WriteAsync(rawData, 0, rawData.Length);
				await networkStream.FlushAsync();

				if (getResponse)
				{
					NetworkData result = await ProtoSerialiser.Unpack(networkStream);
					return result;
				}
			}
			catch (Exception ex)
			{
				if (logException)
				{
					Debug.LogException(ex);
				}
			}

			return null;
		}

		private void OnDestroy()
		{
			_gameConnection?.Disconnect();
		}

		public static void Disconnect()
		{
			_gameConnection?.Disconnect();
		}

		/// <summary>
		/// 	Read and invoke P2P packets sent though Steam.<br/>
		/// 	TODO: Create a new class.
		/// </summary>
		private async void P2PPacketListener()
		{
			_getP2PPacketsThreadActive = true;

			// Create pointer array and put data in it.
			IntPtr[] ptr = new IntPtr[1];
			int msgSize = SteamNetworkingMessages.ReceiveMessagesOnChannel(0, ptr, 1);

			if (msgSize == 0)
			{
				return;
			}

			try
			{
				SteamNetworkingMessage_t netMessage = Marshal.PtrToStructure<SteamNetworkingMessage_t>(ptr[0]);
				if (netMessage.m_cbSize != 0)
				{
					// Copy payload of the message and put it in a byte array.
					byte[] message = new byte[netMessage.m_cbSize];
					Marshal.Copy(netMessage.m_pData, message, 0, message.Length);

					// Deseralise data and invoke code.
					NetworkData networkData = await ProtoSerialiser.Unpack(new MemoryStream(message));
					Debug.Log(networkData);
					if (networkData is ISteamP2PMessage)
					{
						EventSystem.Invoke(networkData);
					}
				}
			}
			finally
			{
				Marshal.DestroyStructure<SteamNetworkingMessage_t>(ptr[0]);
			}

			_getP2PPacketsThreadActive = false;
		}
#if DEBUG
		public static void LogReceivedNetworkData(Type type)
		{
			Instance._receivedLog.Enqueue(new Tuple<float, Type>(
				(float)(DateTime.UtcNow.ToUniversalTime() - Instance._clientStartTime).TotalSeconds, type));
			while (Instance._receivedLog.Count > MaxNetworkDataLogsSize)
			{
				Instance._receivedLog.TryDequeue(out _);
			}
		}

		public static void LogSentNetworkData(Type type)
		{
			Instance._sentLog.Enqueue(new Tuple<float, Type>(
				(float)(DateTime.UtcNow.ToUniversalTime() - Instance._clientStartTime).TotalSeconds, type));
			while (Instance._sentLog.Count > MaxNetworkDataLogsSize)
			{
				Instance._sentLog.TryDequeue(out var _);
			}
		}

		public static string GetNetworkDataLogs()
		{
			if (Instance._receivedLog.IsEmpty || Instance._sentLog.IsEmpty) return "";

			Tuple<float, Type>[] source = Instance._receivedLog.ToArray();
			float lastRecvdTime = source.Last().Item1;
			IEnumerable<Tuple<float, Type>> recvd =
				source.Where((Tuple<float, Type> m) => lastRecvdTime - m.Item1 <= 300f);
			float item = recvd.First().Item1;
			string text = "Received packets (in last " + (lastRecvdTime - item).ToString("0") + "s):\n";
			text += string.Join("\n", from z in (from x in recvd.Select((Tuple<float, Type> m) => m.Item2).Distinct()
					select new Tuple<string, int>(x.Name, recvd.Count((Tuple<float, Type> n) => n.Item2 == x))
					into y
					orderby y.Item2
					select y).Reverse()
				select z.Item1 + ": " + z.Item2);

			Tuple<float, Type>[] source2 = Instance._sentLog.ToArray();
			float lastSentTime = source2.Last().Item1;
			IEnumerable<Tuple<float, Type>> sent =
				source2.Where((Tuple<float, Type> m) => lastSentTime - m.Item1 <= 300f);
			float item2 = sent.First().Item1;
			text = text + "\n\nSent packets (in last " + (lastSentTime - item2).ToString("0") + "s):\n";
			return text + string.Join("\n",
				from z in (from x in sent.Select((Tuple<float, Type> m) => m.Item2).Distinct()
					select new Tuple<string, int>(x.Name, sent.Count((Tuple<float, Type> n) => n.Item2 == x))
					into y
					orderby y.Item2
					select y).Reverse()
				select z.Item1 + ": " + z.Item2);
		}
	}
#endif
}
