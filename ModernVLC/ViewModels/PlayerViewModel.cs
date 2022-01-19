﻿using LibVLCSharp.Shared;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Uwp.UI;
using ModernVLC.Services;
using System;
using System.Linq;
using System.Windows.Input;
using Windows.Media;
using Windows.Media.Devices;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace ModernVLC.ViewModels
{
    internal partial class PlayerViewModel : ObservableObject, IDisposable
    {
        public ICommand PlayPauseCommand { get; private set; }
        public ICommand SeekCommand { get; private set; }
        public ICommand SetTimeCommand { get; private set; }
        public ICommand FullscreenCommand { get; private set; }
        public ICommand SetAudioTrackCommand { get; private set; }
        public ICommand SetSubtitleCommand { get; private set; }
        public ICommand AddSubtitleCommand { get; private set; }
        public ICommand SetPlaybackSpeedCommand { get; private set; }
        public ICommand OpenCommand { get; private set; }
        public ICommand ToggleControlsVisibilityCommand { get; private set; }

        public PlayerService MediaPlayer
        {
            get => _mediaPlayer;
            set => SetProperty(ref _mediaPlayer, value);
        }

        public string MediaTitle
        {
            get => _mediaTitle;
            set => SetProperty(ref _mediaTitle, value);
        }

        public bool IsFullscreen
        {
            get => _isFullscreen;
            private set => SetProperty(ref _isFullscreen, value);
        }

        public bool ControlsHidden
        {
            get => _controlsHidden;
            set => SetProperty(ref _controlsHidden, value);
        }

        public Control VideoView { get; set; }

        private readonly DispatcherQueue DispatcherQueue;
        private readonly DispatcherQueueTimer DispatcherTimer;
        private readonly SystemMediaTransportControls TransportControl;
        private Media _media;
        private string _mediaTitle;
        private PlayerService _mediaPlayer;
        private bool _isFullscreen;
        private bool _controlsHidden;
        private bool _hideControlsManually;
        private CoreCursor _cursor;

        public PlayerViewModel()
        {
            DispatcherQueue = DispatcherQueue.GetForCurrentThread();
            TransportControl = SystemMediaTransportControls.GetForCurrentView();
            DispatcherTimer = DispatcherQueue.CreateTimer();
            PlayPauseCommand = new RelayCommand(PlayPause);
            SeekCommand = new RelayCommand<long>(Seek, (long _) => MediaPlayer.IsSeekable);
            SetTimeCommand = new RelayCommand<RangeBaseValueChangedEventArgs>(SetTime);
            FullscreenCommand = new RelayCommand<bool>(SetFullscreen);
            SetAudioTrackCommand = new RelayCommand<int>(SetAudioTrack);
            SetSubtitleCommand = new RelayCommand<int>(SetSubtitle);
            SetPlaybackSpeedCommand = new RelayCommand<float>(SetPlaybackSpeed);
            OpenCommand = new RelayCommand<object>(Open);
            ToggleControlsVisibilityCommand = new RelayCommand<double>(ToggleControlsVisibility);

            MediaDevice.DefaultAudioRenderDeviceChanged += MediaDevice_DefaultAudioRenderDeviceChanged;
            TransportControl.ButtonPressed += TransportControl_ButtonPressed;
            InitSystemTransportControls();
        }

        private void Open(object value)
        {
            var libVlc = App.DerivedCurrent.LibVLC;
            var uri = value as Uri ?? (value is string path ? new Uri(path) : null);
            if (uri == null) return;

            MediaTitle = uri.Segments.LastOrDefault();
            var oldMedia = _media;
            var media = _media = new Media(libVlc, uri);
            MediaPlayer.Play(media);
            oldMedia?.Dispose();
        }

        private void SetPlaybackSpeed(float speed)
        {
            if (speed != MediaPlayer.Rate)
            {
                MediaPlayer.SetRate(speed);
            }
        }

        private void SetSubtitle(int index)
        {
            if (MediaPlayer.Spu != index)
            {
                MediaPlayer.SetSpu(index);
            }
        }

        private void SetAudioTrack(int index)
        {
            if (MediaPlayer.AudioTrack != index)
            {
                MediaPlayer.SetAudioTrack(index);
            }
        }

        private void MediaDevice_DefaultAudioRenderDeviceChanged(object sender, DefaultAudioRenderDeviceChangedEventArgs args)
        {
            if (args.Role == AudioDeviceRole.Default)
            {
                MediaPlayer.SetOutputDevice(MediaPlayer.OutputDevice);
            }
        }

        private void SetFullscreen(bool value)
        {
            var view = ApplicationView.GetForCurrentView();
            if (view.IsFullScreenMode && !value)
            {
                view.ExitFullScreenMode();
            }

            if (!view.IsFullScreenMode && value)
            {
                view.TryEnterFullScreenMode();
            }

            IsFullscreen = view.IsFullScreenMode;
        }

        public void Initialize(string[] swapChainOptions)
        {
            var libVlc = App.DerivedCurrent.LibVLC;
            if (libVlc == null)
            {
                App.DerivedCurrent.LibVLC = libVlc = new LibVLC(enableDebugLogs: true, swapChainOptions);
            }
            
            MediaPlayer = new PlayerService(libVlc);
            RegisterMediaPlayerPlaybackEvents();
        }

        public void Dispose()
        {
            _media?.Dispose();
            MediaPlayer.Dispose();
            TransportControl.PlaybackStatus = MediaPlaybackStatus.Closed;
        }

        private void PlayPause()
        {
            if (MediaPlayer.IsPlaying && MediaPlayer.CanPause)
            {
                MediaPlayer.Pause();
            }

            if (!MediaPlayer.IsPlaying && MediaPlayer.WillPlay)
            {
                MediaPlayer.Play();
            }

            if (MediaPlayer.State == VLCState.Ended)
            {
                MediaPlayer.Replay();
            }
        }

        private void Seek(long amount)
        {
            if (MediaPlayer.IsSeekable)
            {
                MediaPlayer.Time += amount;
            }
        }

        public void SetInteracting(bool interacting)
        {
            MediaPlayer.ShouldUpdateTime = !interacting;
        }

        public bool JumpFrame(bool previous = false)
        {
            if (MediaPlayer.State == VLCState.Paused && MediaPlayer.IsSeekable)
            {
                if (previous)
                {
                    MediaPlayer.Time -= MediaPlayer.FrameDuration;
                }
                else
                {
                    MediaPlayer.NextFrame();
                }

                return true;
            }

            return false;
        }

        public void ToggleControlsVisibility(double delayInSeconds)
        {
            if (delayInSeconds <= 0)
            {
                if (ControlsHidden)
                {
                    ControlsHidden = false;
                    _hideControlsManually = false;
                }
                else if (MediaPlayer.IsPlaying)
                {
                    ControlsHidden = true;
                    _hideControlsManually = true;
                }
            }
            else
            {
                var coreWindow = Window.Current.CoreWindow;
                if (coreWindow.PointerCursor == null)
                {
                    coreWindow.PointerCursor = _cursor;
                }

                if (_hideControlsManually) return;
                if (ControlsHidden) ControlsHidden = false;

                if (!MediaPlayer.ShouldUpdateTime) return;
                DispatcherTimer.Debounce(() =>
                {
                    if (MediaPlayer.IsPlaying && VideoView.FocusState != FocusState.Unfocused)
                    {
                        ControlsHidden = true;
                        if (coreWindow.PointerCursor?.Type == CoreCursorType.Arrow)
                        {
                            _cursor = coreWindow.PointerCursor;
                            coreWindow.PointerCursor = null;
                        }
                    }
                }, TimeSpan.FromSeconds(delayInSeconds));
            }
        }

        public void OnPointerExited() => _hideControlsManually = false;

        private void SetTime(RangeBaseValueChangedEventArgs args)
        {
            if (MediaPlayer.IsSeekable)
            {
                if ((args.OldValue == MediaPlayer.Time || !MediaPlayer.IsPlaying) &&
                    args.NewValue != MediaPlayer.Length)
                {
                    if (MediaPlayer.State == VLCState.Ended)
                    {
                        MediaPlayer.Replay();
                    }

                    MediaPlayer.Time = (long)args.NewValue;
                    return;
                }

                if (!MediaPlayer.ShouldUpdateTime && args.NewValue != MediaPlayer.Length)
                {
                    DispatcherTimer.Debounce(() => MediaPlayer.Time = (long)args.NewValue, TimeSpan.FromMilliseconds(300));
                    return;
                }
            }
        }
    }
}
