# Docoppolis Web Server (C#)

A lightweight, educational HTTP web server written entirely in C#.  
This project was built as an exploration into low-level server design, routing, session management, and authentication — inspired by the classic [CodeProject tutorial *“Writing a Web Server from Scratch”*](https://www.codeproject.com/articles/Writing-a-Web-Server-from-Scratch), but restructured and implemented using a more modular and maintainable architecture.

---

## 📖 Overview

**Docoppolis Web Server** is a from-scratch implementation of a simple HTTP server using the `HttpListener` class in .NET.  
It handles:

- Static file serving (HTML, CSS, JS, images)
- Dynamic routing for GET, POST, PUT, etc.
- Session management via cookies
- Basic authentication and authorization
- CSRF token validation for form submissions
- Error handling and HTTP response abstraction

The server is designed to serve static web pages and support lightweight dynamic interactions such as login forms, dashboards, and AJAX calls.

---

## ⚙️ Features

- **Routing system** – supports anonymous, authenticated, and expirable routes  
- **Session management** – cookies track user sessions, with timeout support  
- **Authentication & authorization** – simple role-based logic (user/admin)  
- **CSRF protection** – hidden validation tokens inserted automatically  
- **Custom error pages** – friendly HTML responses for missing or restricted content  
- **Static file serving** – built-in support for HTML, JS, CSS, and image assets  

---

## 🧩 Project Structure

Below is a simplified view of the repository layout (replicating the screenshot’s structure):

```
Docoppolis-Web-Server/
│
├── Program.cs                 # Entry point; registers routes and starts the server
│
├── HttpListener.cs            # Core server loop handling connections and responses
├── Router.cs                  # Routing logic; handles dynamic and static routes
├── ResponsePacket.cs          # Unified HTTP response wrapper
├── SessionManager.cs          # Session handling via cookies
│
├── AuthContext.cs             # Stores user authentication context
├── AuthDecision.cs            # Enum for allow/redirect/forbidden auth outcomes
│
├── RequestHelpers.cs          # Query/body parsing utilities
├── StringExtensions.cs        # Path and string helpers
├── Path.cs                    # Resolves file paths for the website root
├── ServerError.cs             # Error type enumeration
│
├── Website/
│   ├── Pages/
│   │   ├── index.html
│   │   ├── login.html
│   │   ├── dashboard.html
│   │   └── admin.html
│   │
│   ├── Scripts/
│   │   └── *.js
│   ├── CSS/
│   │   └── *.css
│   ├── Images/
│   │   └── *.png / *.ico
│   └── ErrorPages/
│       └── *.html
│
└── Screenshot_1.png (file structure reference)
```

---

## 🚀 Getting Started

### Prerequisites
- .NET 6 or later  
- A terminal or IDE such as Visual Studio / VS Code  

### Running the Server
```bash
dotnet run
```

By default, the server listens on:
```
http://localhost:8080/
```

Then open any browser and navigate to:
- `/login` → Login page  
- `/dashboard` → User dashboard (requires login)  
- `/admin` → Admin page (requires admin role)  

Credentials used in this example:
```
user / user
admin / admin
```

---

## 🧠 Technical Overview

### Server Flow
1. **`Program.cs`** registers routes and starts the listener.  
2. **`HttpListener.cs`** accepts incoming requests, resolves sessions, and dispatches them to the **`Router`**.  
3. **`Router.cs`** decides whether to return static content (HTML/CSS/JS/images) or invoke a route handler.  
4. **`SessionManager.cs`** handles session creation and lookup through cookies.  
5. **`ResponsePacket.cs`** standardizes the outgoing response for all handlers.  
6. **CSRF protection** is handled automatically in `PostProcess()`, injecting hidden tokens into HTML forms.

---

## 🧩 Example Routes

From `Program.cs`:

```csharp
Server.AddRoute("GET", "/login", (req, session, qs) => { ... });
Server.AddRoute("POST", "/login", (req, session, qs) => { ... });
Server.AddRoute("GET", "/dashboard", new AuthenticatedExpirableRouteHandler(...));
Server.AddRoute("GET", "/admin", new AuthenticatedExpirableRouteHandler(...));
```

Each route can require authentication, manage sessions, and respond dynamically.

---

## 🏗️ Design Notes

While this project began as an exercise inspired by the CodeProject tutorial, the structure and implementation differ significantly:
- Uses a **modular architecture** with dedicated classes for routing, sessions, and utilities.  
- Implements **CSRF protection**, which was not part of the original tutorial.  
- Refactors route handling to support **custom handler classes** (Anonymous, Authenticated, Expirable).  
- Uses **ResponsePacket abstraction** instead of raw string responses.  

This makes the project a more maintainable, educational reference for how modern frameworks handle these concepts under the hood.

---

## 🙏 Credits & Inspiration

This project was **inspired by the tutorial**  
[*Writing a Web Server from Scratch* by Marc Clifton on CodeProject](https://www.codeproject.com/articles/Writing-a-Web-Server-from-Scratch).

While the core idea and learning path were influenced by the tutorial,  
this implementation was independently designed and restructured to:
- Reinforce personal understanding of web server internals,  
- Explore different architectural and authentication approaches, and  
- Serve as a portfolio piece demonstrating practical C# backend design.

---

## 🧾 License

This project is provided for educational and demonstration purposes.  
You are free to clone, modify, and experiment with it for personal learning.

---

## 📬 Contact

**Author:** John Parrott  
**Purpose:** Portfolio / educational project demonstrating backend fundamentals  
**LinkedIn:** [linkedin.com/in/john-parrott](https://www.linkedin.com/in/john-parrott-6b88ba27a/)
**GitHub:** [github.com/docoppolis](https://github.com/Docoppolis)
