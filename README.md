# Matrix3dengine
Matrix 3D Infinite Canvas Engine  A C# 3D console render engine with an inverted white theme, TCP multiplayer, sandbox commands (/tp, /kill), and a live HUD. Features relative WASD movement based on where you look using a Mouse or Arrow keys. Perfect for infinite custom scene building!
✨ Features

    🗺️ Infinite Canvas: The environment spans infinitely, allowing you to travel through coordinates unbound by map borders.

    🖱️ Hybrid Camera Look: Smoothly rotate your view using your Mouse or use the classic Arrow Keys.

    🧭 True Relative Movement: Pressing WASD moves your character exactly where your camera is looking using pure trigonometric vector matrices.

    🌐 Built-in TCP Multiplayer Server: Host a room or join a friend's IP address to run around the same coordinates in real-time.

    💬 Overhead Chat & Console Commands: Type /tp X Y Z to teleport anywhere in the endless void, or use /kill to reset your coordinates.

    📊 Live HUD Overlay: Displays your current X, Y, Z coordinates and online player counts at the top of the screen in real-time.

🎮 Controls

    W / S — Move Forward / Backward (relative to view)

    A / D — Strafe Left / Right (relative to view)

    Mouse / Arrow Keys — Look Around / Rotate Camera Angle

    Enter — Open Command / Chat Input Prompt

🚀 Quick Start

Ensure you have the .NET SDK installed, then run:
Bash

dotnet run

Choose h to Host a new server locally, or j to join a friend's hosted IP address!
