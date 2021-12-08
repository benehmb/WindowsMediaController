﻿using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Media.Control;

namespace WindowsMediaController
{
    public static class MediaManager
    {
        public delegate void SourceChangeDelegate(MediaSession session);
        public delegate void PlaybackChangeDelegate(MediaSession session, GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo);
        public delegate void SongChangeDelegate(MediaSession session, GlobalSystemMediaTransportControlsSessionMediaProperties mediaProperties);

        /// <summary>
        /// Triggered when a new media source gets added to the MediaSessions
        /// </summary>
        public static event SourceChangeDelegate OnNewSource;

        /// <summary>
        /// Triggered when a media source gets removed from the MediaSessions
        /// </summary>
        public static event SourceChangeDelegate OnRemovedSource;

        /// <summary>
        /// Triggered when a playback state changes of a MediaSession
        /// </summary>
        public static event PlaybackChangeDelegate OnPlaybackStateChanged;

        /// <summary>
        /// Triggered when a song changes of a MediaSession
        /// </summary>
        public static event SongChangeDelegate OnSongChanged;
        
        /// <summary>
        /// A dictionary of the current MediaSessions
        /// </summary>
        public static Dictionary<string, MediaSession> CurrentMediaSessions = new Dictionary<string, MediaSession>();


        private static bool IsStarted;

        /// <summary>
        /// This starts the MediaManager
        /// This can be changed to a constructor if you don't care for the first few 'new sources' events
        /// </summary>
        public static void Start()
        {
            if (!IsStarted)
            {
                var sessionManager = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().GetAwaiter().GetResult();
                SessionsChanged(sessionManager);
                sessionManager.SessionsChanged += SessionsChanged;
                IsStarted = true;
            }
        }

        private static void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args = null)
        {
            var sessionList = sender.GetSessions();

            foreach (var session in sessionList)
            {
                if (!CurrentMediaSessions.ContainsKey(session.SourceAppUserModelId))
                {
                    MediaSession mediaSession = new MediaSession(session);
                    CurrentMediaSessions[session.SourceAppUserModelId] = mediaSession;
                    OnNewSource?.Invoke(mediaSession);
                    mediaSession.OnSongChange(session);
                }
            }

            IEnumerable<string> currentSessionIds = sessionList.Select(x=> x.SourceAppUserModelId);
            List<MediaSession> sessionsToRemove = new List<MediaSession>();

            foreach(var session in CurrentMediaSessions)
            {
                if (!currentSessionIds.Contains(session.Key))
                {
                    sessionsToRemove.Add(session.Value);
                }
            }

            sessionsToRemove.ForEach(x => x.RemoveSelf());
        }


        private static void RemoveSession(MediaSession mediaSession)
        {
            CurrentMediaSessions.Remove(mediaSession.ControlSession.SourceAppUserModelId); 
            OnRemovedSource?.Invoke(mediaSession);
        }

        public class MediaSession
        {
            public GlobalSystemMediaTransportControlsSession ControlSession;

            public MediaSession(GlobalSystemMediaTransportControlsSession ctrlSession)
            {
                ControlSession = ctrlSession;
                ControlSession.MediaPropertiesChanged += OnSongChange;
                ControlSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
            }


            private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession session, PlaybackInfoChangedEventArgs args = null)
            {
                var props = session.GetPlaybackInfo();
                if (props.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed)
                {
                    RemoveSelf();
                }
                else
                {
                    OnPlaybackStateChanged?.Invoke(this, props);
                }
            }

            internal void RemoveSelf()
            {
                ControlSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                ControlSession.MediaPropertiesChanged -= OnSongChange;
                RemoveSession(this);
            }

            internal async void OnSongChange(GlobalSystemMediaTransportControlsSession session, MediaPropertiesChangedEventArgs args = null)
            {
                OnSongChanged?.Invoke(this, await session.TryGetMediaPropertiesAsync());
            }
        }
    }
}
