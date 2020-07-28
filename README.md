# zocket


While iterating on server code and constantly restarting your server, you may encounter a `ERR_CONNECTION_REFUSED` or `ERR_CONNECTION_RESET` when you attempt to tear down your existing server and start up a new process that binds to the same IP/port.

![image](https://user-images.githubusercontent.com/4734691/88629720-7bc47100-d064-11ea-84c4-fc7bc81d5616.png)

zocket attempts to solve this problem by creating a socket and handing it off to child processes that it spawns. This way the listening socket is never interrupted and new instances of your server can call `accept()` on the same socket. Incidentally, this is the same mechanism used by systemd to bind to a socket and it hand off the socket to your process.

I've thus far only tested wtih WSL2 running Ubuntu. I expect to work on any 64-bit Linux distro using glibc.
```
$ uname -a
Linux DESKTOP-OVRUHKP 4.19.84-microsoft-standard #1 SMP Wed Nov 13 11:44:37 UTC 2019 x86_64 x86_64 x86_64 GNU/Linux
```

## Installation

```
dotnet tool install --global zocket --version "0.1.0-*"
```

## Usage

```
Usage:
  zocket [options] [<command>]

Arguments:
  <command>    The command to execute with zocket [default: dotnet watch run]

Options:
  --port <port>     Port to bind to [default: 9999]
  --version         Show version information
  -?, -h, --help    Show help and usage information
  
  Examples:
  
  zocket
  zocket --port 9999 "dotnet run"
  zocket --port 9999 "dotnet watch run"
  ```
  
  ## Third party licenses
  
  A conscious design decision was to not rely on external dependencies in the `ReloadIntegration` library to avoid issues with conflicting versions of transitive dependencies between user code and the startup hook (and avoid the pains associated with additional *.deps.json* files.
  
  To that effect, I've used code from the excellent library by [Tom Deseyn](https://github.com/tmds), [Tmds.LibC](https://github.com/tmds/Tmds.LibC), which is subject to the following license:
  
  ```
  MIT License

Copyright (c) 2019 Tom Deseyn

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
 
