using System;
using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace _Script
{
    public sealed class ConnectionStart : MonoBehaviour
    {
        private Tugboat _tugboat;

        private void OnEnable()
        {
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void OnClientConnectionState(ClientConnectionStateArgs arg)
        {
            if (arg.ConnectionState == LocalConnectionState.Stopped)
            {
                UnityEditor.EditorApplication.isPlaying = false;
            };
        }

        private void Start()
        {
            _tugboat = GetComponent<Tugboat>();

            if (ParrelSync.ClonesManager.IsClone())
            {
                _tugboat.StartConnection(false);
            }
            else
            {
                _tugboat.StartConnection(true);
                _tugboat.StartConnection(false);
            }
        }
    }
}
