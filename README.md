<div align="center">

# SpinTheBottle

### A Blazor-powered bottle-spin game with server-side game logic

An interactive .NET application featuring animated spins, North/South outcomes, configurable wagers, and Firebase-backed data storage.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Blazor](https://img.shields.io/badge/UI-Blazor-512BD4?logo=blazor&logoColor=white)
![C#](https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white)

</div>

---

## Preview

<!-- Add a screenshot of the running app at this path. -->
<p align="center">
  <img src="https://firebasestorage.googleapis.com/v0/b/huntic-ab5f7.firebasestorage.app/o/Spin%20the%20Bottle%20Neon%20Casino%20Banner.png?alt=media&token=cdcff80c-22a3-445d-90d4-e8c06c80bc94" alt="SpinTheBottle application preview" width="900">
</p>

## About

SpinTheBottle is an interactive web application built with **Blazor Server** and **.NET 10**. Players choose a side, spin the bottle, and see the result animated in the browser. Game outcomes and wager processing are handled by server-side services, with Firebase Realtime Database support for application data.

This repository is also a practical example of organizing a small Blazor application around components, services, configuration, and client-side animation.

## Features

- Interactive bottle animation with JavaScript-based physics
- North/South game outcomes
- Trial-spin flow with a configurable bonus
- Server-side outcome and payout calculation
- Configurable wager limits, multiplier, and house-bias rating
- User balances, bets, and game-round records
- Firebase Realtime Database integration with an in-memory fallback
- Password hashing using PBKDF2

## Built with

| Technology | Use |
|---|---|
| C# and .NET 10 | Application and game services |
| Blazor Server | Interactive web UI |
| Firebase Realtime Database | Optional persistence |
| JavaScript | Bottle-spin animation |
| Bootstrap and CSS | Styling |

## Getting started

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A Firebase Realtime Database, if you want persistent data

### Run locally
