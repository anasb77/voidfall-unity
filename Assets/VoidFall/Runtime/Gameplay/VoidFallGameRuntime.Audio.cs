using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime.Rendering;
using VoidFall.UI;
namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {

        private void SetupAudio()
        {
            _audio = gameObject.AddComponent<ProceduralAudio>();
            if (!HasCommandLineArgument("-vfno-music"))
                _music = gameObject.AddComponent<MusicDirector>();
        }
    }
}
