﻿using Ryujinx.Common;
using Ryujinx.SDL2.Common;
using System;

namespace Ryujinx.Input.SDL2
{
    public class SDL2KeyboardDriver : IGamepadDriver
    {
        public SDL2KeyboardDriver(IWorkItemQueue workItemQueue)
        {
            SDL2Driver.Instance.Initialize(workItemQueue);
        }

        public string DriverName => "SDL2";

        private static readonly string[] _keyboardIdentifers = new string[1] { "0" };

        public ReadOnlySpan<string> GamepadsIds => _keyboardIdentifers;

        public event Action<string> OnGamepadConnected
        {
            add { }
            remove { }
        }

        public event Action<string> OnGamepadDisconnected
        {
            add { }
            remove { }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                SDL2Driver.Instance.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public IGamepad GetGamepad(string id)
        {
            if (!_keyboardIdentifers[0].Equals(id))
            {
                return null;
            }

            return new SDL2Keyboard(this, _keyboardIdentifers[0], "All keyboards");
        }
    }
}