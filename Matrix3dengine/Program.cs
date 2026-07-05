using System;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

// Geometry Engine Storage
float[][] blockVerts = new float[][] {
    new float[] {-1,-1,-1}, new float[] {1,-1,-1}, new float[] {1,1,-1}, new float[] {-1,1,-1},
    new float[] {-1,-1,1}, new float[] {1,-1,1}, new float[] {1,1,1}, new float[] {-1,1,1}
};
int[][] blockEdges = new int[][] {
    new int[] {0,1}, new int[] {1,2}, new int[] {2,3}, new int[] {3,0},
    new int[] {4,5}, new int[] {5,6}, new int[] {6,7}, new int[] {7,4},
    new int[] {0,4}, new int[] {1,5}, new int[] {2,6}, new int[] {3,7}
};

// Custom Asset Management
float[][] customVerts = blockVerts;
int[][] customEdges = blockEdges;
string localModelHash = "DEFAULT";
bool hasCustomModel = false;

// Multiplayer Asset Registries
Dictionary<int, float[][]> remotePlayerVerts = new Dictionary<int, float[][]>();
Dictionary<int, int[][]> remotePlayerEdges = new Dictionary<int, int[][]>();
Dictionary<int, string> remotePlayerHashes = new Dictionary<int, string>();

// Camera / Engine Settings
float camX = 0, camY = 0, camZ = -5;
float camRotX = 0, camRotY = 0;
int width = 80, height = 40;

// Theme Colors 
const string RESET = "\x1b[0m";
const string BG_WHITE = "\x1b[47m";   
const string FG_BLACK = "\x1b[30m";   
const string FG_BLUE = "\x1b[34m";    
const string FG_RED = "\x1b[31m";     
const string FG_MAGENTA = "\x1b[35m"; 

// Networking Variables
StreamWriter? networkWriter = null;
NetworkStream? globalStream = null; 
int myPlayerId = -1;
string myUsername = "Player";
Dictionary<int, float[]> remotePlayers = new Dictionary<int, float[]>();
Dictionary<int, string> playerNames = new Dictionary<int, string>();
Dictionary<int, string> playerMessages = new Dictionary<int, string>();
object networkLock = new object();

string currentLocalMessage = "";

Console.Clear();
Console.WriteLine("=== MATRIX 3D MULTIPLAYER CANVAS + OBJ ENGINE ===");

// --- STEP 1: LOAD CUSTOM OBJ FILE IF PRESENT ---
LoadCustomObjFile();

Console.Write("\nEnter your Username: ");
myUsername = Console.ReadLine()?.Trim().Replace(",", "").Replace("|", "").Replace(":", "") ?? "Player";
if (string.IsNullOrEmpty(myUsername)) myUsername = "Player";

Console.Write("Type 'h' to HOST a public network server or 'j' to JOIN/BROWSE: ");
string choice = Console.ReadLine()?.ToLower() ?? "h";

if (choice == "h")
{
    myPlayerId = 1;
    Task.Run(() => StartServerLoop());
    Task.Run(() => StartUdpBeaconLoop()); 
    ConnectToServer("127.0.0.1");
}
else
{
    string targetConnection = DiscoverLocalServers();
    ConnectToServer(targetConnection);
}

Console.CursorVisible = false;
Console.OutputEncoding = Encoding.UTF8;

// Main Engine Loop
while (true)
{
    if (Console.KeyAvailable)
    {
        ConsoleKey key = Console.ReadKey(true).Key;
        
        if (key == ConsoleKey.Enter)
        {
            Console.SetCursorPosition(0, height + 1);
            Console.Write(RESET + "Enter Chat or Command (/kill, /tp x y z):             ");
            Console.SetCursorPosition(43, height + 1);
            Console.CursorVisible = true;
            string? input = Console.ReadLine()?.Trim();
            Console.CursorVisible = false;
            
            if (!string.IsNullOrEmpty(input))
            {
                if (input.ToLower() == "/kill")
                {
                    camX = 0; camY = 0; camZ = -5;
                    camRotX = 0; camRotY = 0;
                    currentLocalMessage = "* Wasted *";
                }
                else if (input.ToLower().StartsWith("/tp "))
                {
                    try
                    {
                        string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4)
                        {
                            camX = float.Parse(parts[1]);
                            camY = float.Parse(parts[2]);
                            camZ = float.Parse(parts[3]);
                            currentLocalMessage = $"Teleported to {camX}, {camY}, {camZ}";
                        }
                    }
                    catch { currentLocalMessage = "Invalid /tp arguments!"; }
                }
                else
                {
                    currentLocalMessage = input.Replace(",", " ").Replace("|", " ").Replace(":", " ");
                }

                string trackingMsg = currentLocalMessage;
                Task.Run(async () => {
                    await Task.Delay(4000);
                    if (currentLocalMessage == trackingMsg) currentLocalMessage = "";
                    SendNetworkUpdate();
                });
            }
            SendNetworkUpdate();
        }
        else
        {
            float forwardX = MathF.Sin(camRotY);
            float forwardZ = MathF.Cos(camRotY);
            float rightX = MathF.Cos(camRotY);
            float rightZ = -MathF.Sin(camRotY);

            if (key == ConsoleKey.W) { camX += forwardX * 0.5f; camZ += forwardZ * 0.5f; }
            if (key == ConsoleKey.S) { camX -= forwardX * 0.5f; camZ -= forwardZ * 0.5f; }
            if (key == ConsoleKey.A) { camX -= rightX * 0.5f; camZ -= rightZ * 0.5f; }
            if (key == ConsoleKey.D) { camX += rightX * 0.5f; camZ += rightZ * 0.5f; }
            
            if (key == ConsoleKey.LeftArrow)  camRotY -= 0.06f;
            if (key == ConsoleKey.RightArrow) camRotY += 0.06f;
            if (key == ConsoleKey.UpArrow)    camRotX -= 0.06f;
            if (key == ConsoleKey.DownArrow)  camRotX += 0.06f;
            
            SendNetworkUpdate();
        }
    }

    char[,] charBuffer = new char[width, height];
    string[,] colorBuffer = new string[width, height];

    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            charBuffer[x, y] = ' '; 
            colorBuffer[x, y] = BG_WHITE + FG_BLACK;
        }
    }

    int horizonY = height / 2 + (int)(camY * 10f - camRotX * 20f);
    if (horizonY >= 0 && horizonY < height)
    {
        for (int x = 0; x < width; x++)
        {
            charBuffer[x, horizonY] = '_';
            colorBuffer[x, horizonY] = BG_WHITE + FG_BLACK;
        }
    }

    // --- SPAWN POINT ---
    RenderObject(customVerts, customEdges, 0f, 0f, 0f, charBuffer, colorBuffer, FG_BLUE, "[Spawn Point]");

    // --- REMOTE PLAYERS ---
    lock (networkLock)
    {
        foreach (var p in remotePlayers)
        {
            if (p.Key == myPlayerId) continue;
            float[] pos = p.Value;
            
            string nameTag = playerNames.ContainsKey(p.Key) ? playerNames[p.Key] : $"Player {p.Key}";
            string msg = playerMessages.ContainsKey(p.Key) ? playerMessages[p.Key] : "";
            string overhead = string.IsNullOrEmpty(msg) ? nameTag : $"{nameTag}: {msg}";
            
            float[][] activeMeshVerts = remotePlayerVerts.ContainsKey(p.Key) ? remotePlayerVerts[p.Key] : blockVerts;
            int[][] activeMeshEdges = remotePlayerEdges.ContainsKey(p.Key) ? remotePlayerEdges[p.Key] : blockEdges;

            RenderObject(activeMeshVerts, activeMeshEdges, pos[0], pos[1], pos[2], charBuffer, colorBuffer, FG_RED, overhead);
        }
    }

    string status = $"XYZ: {camX:0.0}, {camY:0.0}, {camZ:0.0} | Model: {(hasCustomModel ? "Custom" : "Box")} | Online: {remotePlayers.Count + 1}";
    for (int i = 0; i < status.Length && i < width; i++)
    {
        charBuffer[i, 0] = status[i];
        colorBuffer[i, 0] = BG_WHITE + FG_BLACK;
    }

    string prompt = "[Arrows = Look] | [WASD = Move] | [Enter = Chat]";
    for (int i = 0; i < prompt.Length && i < width; i++) 
    {
        charBuffer[i, height - 1] = prompt[i];
        colorBuffer[i, height - 1] = BG_WHITE + FG_BLACK;
    }

    RenderToScreen(charBuffer, colorBuffer);
    Thread.Sleep(33);
}

void LoadCustomObjFile()
{
    string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom.obj");
    
    // DEBUG UPGRADE: Prints exact runtime folder structure mapping rules
    Console.WriteLine($"[FILE SCAN] Looking for your file here: {filePath}");

    if (!File.Exists(filePath))
    {
        Console.WriteLine("[FILE SCAN] Result: No 'custom.obj' found. Using default box layout.");
        return;
    }

    FileInfo info = new FileInfo(filePath);
    if (info.Length > 2 * 1024 * 1024) 
    {
        Console.WriteLine("[FILE SCAN] Error: File is larger than 2MB! Defaulting to box geometry.");
        return;
    }

    try
    {
        List<float[]> vertices = new List<float[]>();
        List<int[]> edges = new List<int[]>();
        HashSet<string> uniqueEdges = new HashSet<string>();

        foreach (string line in File.ReadLines(filePath))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("v "))
            {
                string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    vertices.Add(new float[] {
                        float.Parse(parts[1]),
                        float.Parse(parts[2]),
                        float.Parse(parts[3])
                    });
                }
            }
            else if (trimmed.StartsWith("f "))
            {
                string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                List<int> faceIndices = new List<int>();
                for (int i = 1; i < parts.Length; i++)
                {
                    string vertexPart = parts[i].Split('/')[0];
                    if (int.TryParse(vertexPart, out int idx))
                    {
                        faceIndices.Add(idx - 1); 
                    }
                }

                for (int i = 0; i < faceIndices.Count; i++)
                {
                    int v1 = faceIndices[i];
                    int v2 = faceIndices[(i + 1) % faceIndices.Count];
                    int min = Math.Min(v1, v2);
                    int max = Math.Max(v1, v2);
                    string key = $"{min}-{max}";
                    if (!uniqueEdges.Contains(key))
                    {
                        uniqueEdges.Add(key);
                        edges.Add(new int[] { v1, v2 });
                    }
                }
            }
        }

        if (vertices.Count > 0 && edges.Count > 0)
        {
            customVerts = vertices.ToArray();
            customEdges = edges.ToArray();
            hasCustomModel = true;

            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                localModelHash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").Substring(0, 8);
            }
            Console.WriteLine($"[FILE SCAN] Success! custom.obj loaded. Checksum: {localModelHash}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FILE SCAN] Error parsing layout properties: {ex.Message}");
        customVerts = blockVerts;
        customEdges = blockEdges;
    }
}

void LoadDownloadedObjMesh(int remoteId, string textContent)
{
    try
    {
        List<float[]> vertices = new List<float[]>();
        List<int[]> edges = new List<int[]>();
        HashSet<string> uniqueEdges = new HashSet<string>();

        string[] lines = textContent.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("v "))
            {
                string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                    vertices.Add(new float[] { float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]) });
            }
            else if (trimmed.StartsWith("f "))
            {
                string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                List<int> faceIndices = new List<int>();
                for (int i = 1; i < parts.Length; i++)
                {
                    string vPart = parts[i].Split('/')[0];
                    if (int.TryParse(vPart, out int idx)) faceIndices.Add(idx - 1);
                }
                for (int i = 0; i < faceIndices.Count; i++)
                {
                    int v1 = faceIndices[i]; int v2 = faceIndices[(i + 1) % faceIndices.Count];
                    int min = Math.Min(v1, v2); int max = Math.Max(v1, v2);
                    if (!uniqueEdges.Contains($"{min}-{max}"))
                    {
                        uniqueEdges.Add($"{min}-{max}");
                        edges.Add(new int[] { v1, v2 });
                    }
                }
            }
        }
        if (vertices.Count > 0 && edges.Count > 0)
        {
            remotePlayerVerts[remoteId] = vertices.ToArray();
            remotePlayerEdges[remoteId] = edges.ToArray();
        }
    }
    catch { }
}

void SendNetworkUpdate()
{
    try 
    { 
        string msgToSend = string.IsNullOrEmpty(currentLocalMessage) ? "NONE" : currentLocalMessage;
        networkWriter?.WriteLine($"{camX},{camY},{camZ},{myUsername},{localModelHash},{msgToSend}"); 
    } 
    catch { }
}

void StartUdpBeaconLoop()
{
    using UdpClient udpServer = new UdpClient();
    udpServer.EnableBroadcast = true;
    IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 12346);

    while (true)
    {
        try
        {
            byte[] data = Encoding.UTF8.GetBytes($"3DSERVER:{myUsername}");
            udpServer.Send(data, data.Length, endPoint);
        }
        catch { }
        Thread.Sleep(1000);
    }
}

string DiscoverLocalServers()
{
    Console.WriteLine("\nScanning network for active game lobbies... (Waiting 3 seconds)");
    List<string> serverIps = new List<string>();
    List<string> serverNames = new List<string>();
    
    using UdpClient udpListener = new UdpClient(12346);
    udpListener.Client.ReceiveTimeout = 3000; 
    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

    DateTime endTime = DateTime.Now.AddSeconds(3);
    while (DateTime.Now < endTime)
    {
        try
        {
            byte[] receivedBytes = udpListener.Receive(ref remoteEP);
            string message = Encoding.UTF8.GetString(receivedBytes);
            
            if (message.StartsWith("3DSERVER:"))
            {
                string hostName = message.Split(':')[1];
                string ipAddress = remoteEP.Address.ToString();

                if (!serverIps.Contains(ipAddress))
                {
                    serverIps.Add(ipAddress);
                    serverNames.Add(hostName);
                    Console.WriteLine($"Found Server -> [{serverIps.Count}] Host: {hostName} (IP: {ipAddress})");
                }
            }
        }
        catch (SocketException) { break; }
    }

    Console.WriteLine("\n=======================================================");
    Console.WriteLine("Options:");
    if (serverIps.Count > 0) Console.WriteLine("- Type the selection NUMBER of a discovered server.");
    Console.WriteLine("- OR type a manual IP Address / PC Hostname (.local)");
    Console.WriteLine("=======================================================");
    Console.Write("Input: ");
    
    string input = Console.ReadLine()?.Trim() ?? "";
    if (string.IsNullOrEmpty(input)) return "127.0.0.1";
    if (int.TryParse(input, out int selection) && selection > 0 && selection <= serverIps.Count) return serverIps[selection - 1];
    return input;
}

void ConnectToServer(string targetHost)
{
    try
    {
        IPAddress[] addresses = Dns.GetHostAddresses(targetHost);
        if (addresses.Length == 0) throw new Exception("Host not found");

        TcpClient client = new TcpClient();
        client.Connect(addresses[0], 12345);

        globalStream = client.GetStream();
        StreamReader reader = new StreamReader(globalStream);
        networkWriter = new StreamWriter(globalStream) { AutoFlush = true };

        Task.Run(() => {
            while (true)
            {
                try
                {
                    string? line = reader.ReadLine();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.StartsWith("ID:"))
                    {
                        myPlayerId = int.Parse(line.Split(':')[1]);
                        SendNetworkUpdate(); 
                        continue;
                    }

                    if (line.StartsWith("ASSET_REQ:"))
                    {
                        string objPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom.obj");
                        if (File.Exists(objPath))
                        {
                            string rawText = File.ReadAllText(objPath).Replace("\r", "").Replace("\n", "§");
                            networkWriter?.WriteLine($"ASSET_DATA:{rawText}");
                        }
                        continue;
                    }

                    if (line.StartsWith("ASSET_DATA:"))
                    {
                        string dataMesh = line.Substring(11).Replace("§", "\n");
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloaded.obj"), dataMesh);
                        continue;
                    }

                    lock (networkLock)
                    {
                        remotePlayers.Clear();
                        playerNames.Clear();
                        playerMessages.Clear();
                        string[] playerPackets = line.Split('|');
                        foreach (var player in playerPackets)
                        {
                            if (string.IsNullOrEmpty(player)) continue;
                            string[] parts = player.Split(':');
                            if (parts.Length < 2) continue; 

                            int id = int.Parse(parts[0]);
                            string[] data = parts[1].Split(',');
                            if (data.Length < 3) continue; 

                            remotePlayers[id] = new float[] {
                                float.Parse(data[0]), float.Parse(data[1]), float.Parse(data[2])
                            };
                            
                            if (data.Length > 3) playerNames[id] = data[3];
                            
                            if (data.Length > 4)
                            {
                                string incomingHash = data[4];
                                if (incomingHash != "DEFAULT" && (!remotePlayerHashes.ContainsKey(id) || remotePlayerHashes[id] != incomingHash))
                                {
                                    remotePlayerHashes[id] = incomingHash;
                                    int targetId = id;
                                    string hostUser = playerNames[id];
                                    Task.Run(() => HandleAssetDownloadQuery(targetId, hostUser));
                                }
                            }

                            if (data.Length > 5 && data[5] != "NONE") playerMessages[id] = data[5];
                        }
                    }
                }
                catch { break; }
            }
        });
    }
    catch
    {
        Console.WriteLine("\nConnection failed!");
        Console.ReadLine();
        Environment.Exit(0);
    }
}

void HandleAssetDownloadQuery(int remoteId, string companionName)
{
    Console.Clear();
    Console.SetCursorPosition(0, 0);
    Console.WriteLine(RESET);
    Console.WriteLine($"=======================================================");
    Console.WriteLine($"ASSET MONITOR: {companionName} is using a custom 3D .OBJ model!");
    Console.WriteLine($"Do you want to download and display their custom look? (y/n)");
    Console.WriteLine($"=======================================================");
    Console.Write("Choice: ");
    
    string choice = Console.ReadLine()?.ToLower().Trim() ?? "n";
    if (choice == "y")
    {
        Console.WriteLine("Requesting asset files from the host...");
        networkWriter?.WriteLine($"FETCH_ASSET:{remoteId}");
        
        Thread.Sleep(1500);
        string downloadedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloaded.obj");
        if (File.Exists(downloadedPath))
        {
            string content = File.ReadAllText(downloadedPath);
            LoadDownloadedObjMesh(remoteId, content);
            Console.WriteLine("Asset download complete and applied!");
        }
    }
    else
    {
        Console.WriteLine("Download skipped. Rendering default box.");
    }
    Thread.Sleep(1000);
    Console.Clear();
}

void StartServerLoop()
{
    System.Collections.Concurrent.ConcurrentDictionary<int, string> serverPlayerPositions = new();
    System.Collections.Concurrent.ConcurrentDictionary<int, StreamWriter> serverWriters = new();
    int idCounter = 0;

    TcpListener listener = new TcpListener(IPAddress.Any, 12345);
    listener.Start();

    while (true)
    {
        TcpClient client = listener.AcceptTcpClient();
        int assignedId = Interlocked.Increment(ref idCounter);
        
        Task.Run(() => {
            using NetworkStream stream = client.GetStream();
            using StreamReader reader = new StreamReader(stream);
            using StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };

            serverWriters[assignedId] = writer;
            serverPlayerPositions[assignedId] = "0,0,-5,Player,DEFAULT,NONE";
            writer.WriteLine($"ID:{assignedId}");

            try
            {
                while (!reader.EndOfStream)
                {
                    string? msg = reader.ReadLine();
                    if (string.IsNullOrEmpty(msg)) continue;

                    if (msg.StartsWith("FETCH_ASSET:"))
                    {
                        int targetId = int.Parse(msg.Split(':')[1]);
                        if (serverWriters.ContainsKey(targetId))
                        {
                            serverWriters[targetId].WriteLine("ASSET_REQ:PLEASE_SEND");
                        }
                        continue;
                    }

                    if (msg.StartsWith("ASSET_DATA:"))
                    {
                        var globalWriters = serverWriters.ToArray();
                        foreach(var w in globalWriters) { w.Value.WriteLine(msg); }
                        continue;
                    }

                    serverPlayerPositions[assignedId] = msg;

                    List<string> packet = new();
                    var positionsSnapshot = serverPlayerPositions.ToArray();
                    foreach (var p in positionsSnapshot) packet.Add($"{p.Key}:{p.Value}");
                    string fullPacket = string.Join("|", packet);

                    var writersSnapshot = serverWriters.ToArray();
                    foreach (var w in writersSnapshot) { try { w.Value.WriteLine(fullPacket); } catch { } }
                }
            }
            catch { }
            finally
            {
                serverWriters.TryRemove(assignedId, out _);
                serverPlayerPositions.TryRemove(assignedId, out _);
            }
        });
    }
}

void RenderObject(float[][] verts, int[][] lines, float ox, float oy, float oz, char[,] cBuf, string[,] colBuf, string color, string overheadMsg)
{
    int[][] proj = new int[verts.Length][];
    float avgScreenX = 0;
    float highestY = 9999; 

    for (int i = 0; i < verts.Length; i++)
    {
        float cx = verts[i][0] + ox - camX;
        float cy = verts[i][1] + oy - camY;
        float cz = verts[i][2] + oz - camZ;

        float cosY = MathF.Cos(-camRotY), sinY = MathF.Sin(-camRotY);
        float rx1 = cx * cosY + cz * sinY;
        float rz1 = -cx * sinY + cz * cosY;

        float cosX = MathF.Cos(-camRotX), sinX = MathF.Sin(-camRotX);
        float ry2 = cy * cosX - rz1 * sinX;
        float rz2 = cy * sinX + rz1 * cosX;

        if (rz2 <= 0.1f) return; 

        int sx = (int)(width / 2 + (rx1 * 50.0f / rz2) * 2.0f);
        int sy = (int)(height / 2 + (ry2 * 50.0f / rz2));
        proj[i] = new int[] { sx, sy };

        avgScreenX += sx;
        if (sy < highestY) highestY = sy;
    }
    
    avgScreenX /= verts.Length;

    foreach (var edge in lines)
    {
        if(edge[0] < proj.Length && edge[1] < proj.Length)
            DrawLine(proj[edge[0]][0], proj[edge[0]][1], proj[edge[1]][0], proj[edge[1]][1], cBuf, colBuf, color);
    }

    if (!string.IsNullOrEmpty(overheadMsg))
    {
        int textX = (int)avgScreenX - (overheadMsg.Length / 2);
        int textY = (int)highestY - 2; 

        if (textY >= 0 && textY < height)
        {
            for (int i = 0; i < overheadMsg.Length; i++)
            {
                int targetX = textX + i;
                if (targetX >= 0 && targetX < width)
                {
                    cBuf[targetX, textY] = overheadMsg[i];
                    colBuf[targetX, textY] = BG_WHITE + FG_MAGENTA; 
                }
            }
        }
    }
}

void DrawLine(int x0, int y0, int x1, int y1, char[,] cBuf, string[,] colBuf, string color)
{
    int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
    int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
    int err = dx + dy, e2;
    while (true)
    {
        if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height) { cBuf[x0, y0] = '#'; colBuf[x0, y0] = color; }
        if (x0 == x1 && y0 == y1) break;
        e2 = 2 * err;
        if (e2 >= dy) { err += dy; x0 += sx; }
        if (e2 <= dx) { err += dx; y0 += sy; }
    }
}

void RenderToScreen(char[,] cBuf, string[,] colBuf)
{
    Console.SetCursorPosition(0, 0);
    StringBuilder sb = new StringBuilder();
    string activeColor = "";
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            if (colBuf[x, y] != activeColor) { activeColor = colBuf[x, y]; sb.Append(activeColor); }
            sb.Append(cBuf[x, y]);
        }
        if (y < height - 1) sb.Append('\n');
    }
    Console.Write(sb.ToString());
}
