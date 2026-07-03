using System;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.IO;

// Lists to hold the uploaded custom 3D model data
List<float[]> customVerts = new List<float[]>();
List<int[]> customEdges = new List<int[]>();
string modelTag = "[No model.obj found]";

// Standard Cube Shape for the Spawn Point Anchor
float[][] spawnVerts = new float[][] {
    new float[] {-1,-1,-1}, new float[] {1,-1,-1}, new float[] {1,1,-1}, new float[] {-1,1,-1},
    new float[] {-1,-1,1}, new float[] {1,-1,1}, new float[] {1,1,1}, new float[] {-1,1,1}
};
int[][] spawnEdges = new int[][] {
    new int[] {0,1}, new int[] {1,2}, new int[] {2,3}, new int[] {3,0},
    new int[] {4,5}, new int[] {5,6}, new int[] {6,7}, new int[] {7,4},
    new int[] {0,4}, new int[] {1,5}, new int[] {2,6}, new int[] {3,7}
};

// Load custom figure if it exists
string objPath = "model.obj";
if (File.Exists(objPath))
{
    LoadObjFile(objPath, customVerts, customEdges);
    modelTag = $"[Loaded: {Path.GetFileName(objPath)}]";
}

// Camera / Position Settings (Starting grounded at Y = 0)
float camX = 0, camY = 0, camZ = -5;
float camRotX = 0, camRotY = 0;
int width = 80, height = 40;

// Jump Physics Variables
float velocityY = 0f;
bool isGrounded = true;
const float GRAVITY = -0.08f; 
const float JUMP_FORCE = 0.6f; 

// Chat System Storage
List<string> chatLog = new List<string>() { "Sandbox Terminal initialized.", "Press [Space] to Jump, [Enter] for Commands." };
int maxChatLines = 5;

// Theme Colors
const string RESET = "\x1b[0m";
const string BG_WHITE = "\x1b[47m";   
const string FG_BLACK = "\x1b[30m";   
const string FG_BLUE = "\x1b[34m";    
const string FG_RED = "\x1b[31m";     
const string FG_MAGENTA = "\x1b[35m"; 

int lastMouseX = -1, lastMouseY = -1;

Console.Clear();
Console.CursorVisible = false;
Console.OutputEncoding = Encoding.UTF8;

// Main Engine Loop
while (true)
{
    // --- JUMP PHYSICS UPDATE ---
    if (!isGrounded)
    {
        velocityY += GRAVITY;
        camY += velocityY;

        if (camY <= 0f)
        {
            camY = 0f;
            velocityY = 0f;
            isGrounded = true;
        }
    }

    // Mouse Look Control
    try
    {
        if (Console.WindowWidth > 0 && Console.WindowHeight > 0)
        {
            int mouseX = Console.CursorLeft;
            int mouseY = Console.CursorTop;
            if (lastMouseX == -1) { lastMouseX = mouseX; lastMouseY = mouseY; }

            if (mouseX != lastMouseX || mouseY != lastMouseY)
            {
                camRotY += (mouseX - lastMouseX) * 0.01f;
                camRotX += (mouseY - lastMouseY) * 0.01f;
            }
            lastMouseX = mouseX; lastMouseY = mouseY;
        }
    } 
    catch {}

    // Input Processing
    if (Console.KeyAvailable)
    {
        ConsoleKey key = Console.ReadKey(true).Key;
        
        if (key == ConsoleKey.Enter) 
        {
            Console.SetCursorPosition(0, height + 1);
            Console.Write(RESET + "Chat / Command:                                                 ");
            Console.SetCursorPosition(16, height + 1);
            Console.CursorVisible = true;
            string? input = Console.ReadLine()?.Trim();
            Console.CursorVisible = false;

            if (!string.IsNullOrEmpty(input))
            {
                string lowerInput = input.ToLower();
                if (lowerInput == "/kill" || lowerInput == "/tp spawn")
                {
                    camX = 0; camY = 0; camZ = -5;
                    camRotX = 0; camRotY = 0;
                    velocityY = 0; isGrounded = true;
                    chatLog.Add(lowerInput == "/kill" ? "* Wasted *" : "Teleported back to Spawn Point.");
                }
                else if (lowerInput.StartsWith("/tp "))
                {
                    try
                    {
                        string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4)
                        {
                            camX = float.Parse(parts[1]);
                            camY = float.Parse(parts[2]);
                            camZ = float.Parse(parts[3]);
                            chatLog.Add($"Teleported to {camX}, {camY}, {camZ}");
                        }
                    }
                    catch { chatLog.Add("System: Invalid /tp arguments!"); }
                }
                else
                {
                    chatLog.Add($"You: {input}");
                }

                if (chatLog.Count > maxChatLines) chatLog.RemoveAt(0);
            }
            
            Console.SetCursorPosition(0, height + 1);
            Console.Write("                                                                   ");
        }
        else if (key == ConsoleKey.Spacebar) 
        {
            if (isGrounded)
            {
                velocityY = JUMP_FORCE;
                isGrounded = false;
            }
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
        }
    }

    // Canvas Frame Setup
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

    // 1. Render blue Spawn Point Box at (0, 0, 0)
    RenderObject(spawnVerts, spawnEdges, 0f, 0f, 0f, charBuffer, colorBuffer, FG_BLUE, "[Spawn Point]");

    // 2. Render your Custom Figure near the spawn point at (0, 0, 3)
    if (customVerts.Count > 0)
    {
        RenderObject(customVerts.ToArray(), customEdges.ToArray(), 0f, 0f, 3f, charBuffer, colorBuffer, FG_RED, modelTag);
    }

    // Live HUD Overlay
    string status = $"XYZ: {camX:0.0}, {camY:0.0}, {camZ:0.0} | Space = Jump | Enter = Commands";
    for (int i = 0; i < status.Length && i < width; i++) charBuffer[i, 0] = status[i];

    // Chat Window Overlay
    int startChatY = height - 1 - maxChatLines;
    for (int i = 0; i < chatLog.Count; i++)
    {
        string currentLine = chatLog[i];
        int targetY = startChatY + i;
        if (targetY >= 0 && targetY < height)
        {
            for (int x = 0; x < currentLine.Length && x < width; x++)
            {
                charBuffer[x, targetY] = currentLine[x];
                colorBuffer[x, targetY] = BG_WHITE + FG_MAGENTA;
            }
        }
    }

    RenderToScreen(charBuffer, colorBuffer);
    Thread.Sleep(33);
}

// OBJ Parser Logic Engine with Auto-Centering Bounds Math
void LoadObjFile(string path, List<float[]> verts, List<int[]> edges)
{
    string[] lines = File.ReadAllLines(path);
    HashSet<string> uniqueEdges = new HashSet<string>();
    List<float[]> rawVerts = new List<float[]>();

    float minX = float.MaxValue, maxX = float.MinValue;
    float minY = float.MaxValue, maxY = float.MinValue;
    float minZ = float.MaxValue, maxZ = float.MinValue;

    foreach (string line in lines)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith("v ")) 
        {
            string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            float x = float.Parse(parts[1]);
            float y = float.Parse(parts[2]);
            float z = float.Parse(parts[3]);
            
            rawVerts.Add(new float[] { x, y, z });

            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
            if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
        }
        else if (trimmed.StartsWith("f ")) 
        {
            string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            List<int> faceIndices = new List<int>();

            for (int i = 1; i < parts.Length; i++)
            {
                string vertPart = parts[i].Split('/')[0]; 
                faceIndices.Add(int.Parse(vertPart) - 1); 
            }

            for (int i = 0; i < faceIndices.Count; i++)
            {
                int vA = faceIndices[i];
                int vB = faceIndices[(i + 1) % faceIndices.Count];

                int min = Math.Min(vA, vB);
                int max = Math.Max(vA, vB);
                string edgeKey = $"{min}-{max}";

                if (!uniqueEdges.Contains(edgeKey))
                {
                    uniqueEdges.Add(edgeKey);
                    edges.Add(new int[] { vA, vB });
                }
            }
        }
    }

    float centerX = (minX + maxX) / 2f;
    float centerY = (minY + maxY) / 2f;
    float centerZ = (minZ + maxZ) / 2f;

    foreach (var v in rawVerts)
    {
        verts.Add(new float[] { v[0] - centerX, v[1] - centerY, v[2] - centerZ });
    }
}

void RenderObject(float[][] verts, int[][] lines, float ox, float oy, float oz, char[,] cBuf, string[,] colBuf, string color, string tag)
{
    int[][] proj = new int[verts.Length][];
    float avgScreenX = 0; float highestY = 9999; 

    for (int i = 0; i < verts.Length; i++)
    {
        float cx = verts[i][0] + ox - camX; float cy = verts[i][1] + oy - camY; float cz = verts[i][2] + oz - camZ;
        float cosY = MathF.Cos(-camRotY), sinY = MathF.Sin(-camRotY);
        float rx1 = cx * cosY + cz * sinY; float rz1 = -cx * sinY + cz * cosY;
        float cosX = MathF.Cos(-camRotX), sinX = MathF.Sin(-camRotX);
        float ry2 = cy * cosX - rz1 * sinX; 
        float rz2 = cy * sinX + rz1 * cosX; // Fixed typo here (added missing float type descriptor)

        if (rz2 <= 0.1f) return; 
        
        int sx = (int)(width / 2 + (rx1 * 60.0f / rz2) * 2.5f);
        int sy = (int)(height / 2 + (ry2 * 60.0f / rz2));
        proj[i] = new int[] { sx, sy };
        avgScreenX += sx; if (sy < highestY) highestY = sy;
    }
    avgScreenX /= verts.Length;
    foreach (var edge in lines) DrawLine(proj[edge[0]][0], proj[edge[0]][1], proj[edge[1]][0], proj[edge[1]][1], cBuf, colBuf, color);

    if (!string.IsNullOrEmpty(tag))
    {
        int textX = (int)avgScreenX - (tag.Length / 2); int textY = (int)highestY - 2; 
        if (textY >= 0 && textY < height && textX >= 0 && textX + tag.Length < width)
        {
            for (int i = 0; i < tag.Length; i++)
            {
                cBuf[textX + i, textY] = tag[i];
                colBuf[textX + i, textY] = BG_WHITE + color;
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
    StringBuilder sb = new StringBuilder(); string activeColor = "";
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            if (colBuf[x, y] != activeColor) { activeColor = colBuf[x, y]; sb.Append(activeColor); }
            sb.Append(cBuf[x, y]);
        }
        sb.Append('\n');
    }
    Console.Write(sb.ToString());
}
