﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace WindowsMediaController
{
    public class MediaManager : IDisposable
    {
        public delegate void SourceChangeDelegate(MediaSession session);
        public delegate void PlaybackChangeDelegate(MediaSession session, GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo);
        public delegate void SongChangeDelegate(MediaSession session, GlobalSystemMediaTransportControlsSessionMediaProperties mediaProperties);

        /// <summary>
        /// Triggered when a new media source gets added to the MediaSessions
        /// </summary>
        public event SourceChangeDelegate OnNewSource;

        /// <summary>
        /// Triggered when a media source gets removed from the MediaSessions
        /// </summary>
        public event SourceChangeDelegate OnRemovedSource;

        /// <summary>
        /// Triggered when a playback state changes of a MediaSession
        /// </summary>
        public event PlaybackChangeDelegate OnPlaybackStateChanged;

        /// <summary>
        /// Triggered when a song changes of a MediaSession
        /// </summary>
        public event SongChangeDelegate OnSongChanged;
        
        /// <summary>
        /// A dictionary of the current MediaSessions
        /// </summary>
        public Dictionary<string, MediaSession> CurrentMediaSessions = new Dictionary<string, MediaSession>();


        private bool IsStarted;
        private GlobalSystemMediaTransportControlsSessionManager windowsSessionManager;

        /// <summary>
        /// This starts the MediaManager
        /// This can be changed to a constructor if you don't care for the first few 'new sources' events
        /// </summary>
        public async Task Start()
        {
            if (!IsStarted)
            {
                windowsSessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                SessionsChanged(windowsSessionManager);
                windowsSessionManager.SessionsChanged += SessionsChanged;
                IsStarted = true;
            }
            else
            {
                throw new InvalidOperationException("MediaManager already started");
            }
        }

        private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager winSessionManager, SessionsChangedEventArgs args = null)
        {
            var controlSessionList = winSessionManager.GetSessions();

            //Checking for any new sessions, if found a new one add it to our dictiony and fire OnNewSource
            foreach (var controlSession in controlSessionList)
            {
                if (!CurrentMediaSessions.ContainsKey(controlSession.SourceAppUserModelId))
                {
                    MediaSession mediaSession = new MediaSession(controlSession, this);
                    CurrentMediaSessions[controlSession.SourceAppUserModelId] = mediaSession;
                    OnNewSource?.Invoke(mediaSession);
                    mediaSession.OnSongChange(controlSession);
                }
            }

            //Checking if a source fell off the session list without doing a proper Closed event (*cough* spotify *cough*)
            IEnumerable<string> controlSessionIds = controlSessionList.Select(x=> x.SourceAppUserModelId);
            List<MediaSession> sessionsToRemove = new List<MediaSession>();

            foreach(var session in CurrentMediaSessions)
            {
                if (!controlSessionIds.Contains(session.Key))
                {
                    sessionsToRemove.Add(session.Value);
                }
            }

            sessionsToRemove.ForEach(x => x.Dispose());
        }


        private void RemoveSource(MediaSession mediaSession)
        {
            CurrentMediaSessions.Remove(mediaSession.ControlSession.SourceAppUserModelId); 
            OnRemovedSource?.Invoke(mediaSession);
        }

        public void StopAndReset() 
        {
            if (IsStarted)
            {
                Dispose();
            }
            else
            {
                throw new InvalidOperationException("MediaManager did not start yet");
            }
        }

        public void Dispose()
        {
            OnNewSource = null;
            OnRemovedSource = null;
            OnSongChanged = null;
            OnPlaybackStateChanged = null;

            foreach (var mediaSession in CurrentMediaSessions)
            {
                mediaSession.Value.Dispose();
            }
            CurrentMediaSessions?.Clear();

            IsStarted = false;
            windowsSessionManager.SessionsChanged -= SessionsChanged;
            windowsSessionManager = null;
        }

        public class MediaSession : IDisposable
        {
            public GlobalSystemMediaTransportControlsSession ControlSession;
            internal MediaManager MediaManagerInstance;

            internal MediaSession(GlobalSystemMediaTransportControlsSession controlSession, MediaManager mediaMangerInstance)
            {
                MediaManagerInstance = mediaMangerInstance;
                ControlSession = controlSession;
                ControlSession.MediaPropertiesChanged += OnSongChange;
                ControlSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
            }


            private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession controlSession, PlaybackInfoChangedEventArgs args = null)
            {
                var playbackInfo = controlSession.GetPlaybackInfo();

                if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed)
                {
                    Dispose();
                }
                else
                {
                    MediaManagerInstance.OnPlaybackStateChanged?.Invoke(this, playbackInfo);
                }
            }

            internal async void OnSongChange(GlobalSystemMediaTransportControlsSession controlSession, MediaPropertiesChangedEventArgs args = null)
            {
                MediaManagerInstance.OnSongChanged?.Invoke(this, await controlSession.TryGetMediaPropertiesAsync());
            }

            public void Dispose()
            {
                ControlSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                ControlSession.MediaPropertiesChanged -= OnSongChange;
                MediaManagerInstance.RemoveSource(this);
            }
        }
    }
}