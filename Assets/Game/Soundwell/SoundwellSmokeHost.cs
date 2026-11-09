#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

namespace Rpg.Soundwell
{
    // Runs only inside the isolated CI Play Mode session.
    public sealed class SoundwellSmokeHost : MonoBehaviour
    {
        private string runtimeError;

        public void Run(IEnumerator steps, Action<int> finish)
        {
            Application.logMessageReceived += RecordRuntimeError;
            StartCoroutine(Drive(steps, finish));
        }

        private IEnumerator Drive(IEnumerator steps, Action<int> finish)
        {
            int status = 1;
            while (true)
            {
                bool more;
                try
                {
                    if (runtimeError != null)
                        throw new InvalidOperationException(runtimeError);
                    more = steps.MoveNext();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    break;
                }

                if (!more)
                {
                    if (runtimeError == null)
                    {
                        Debug.Log("RPG_SOUNDWELL_SIGNAL_VALIDATED");
                        status = 0;
                    }
                    break;
                }
                yield return steps.Current;
            }

            Application.logMessageReceived -= RecordRuntimeError;
            finish(status);
        }

        private void RecordRuntimeError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                runtimeError ??= message;
        }

        private void OnDestroy() => Application.logMessageReceived -= RecordRuntimeError;
    }
}
#endif
