﻿using DataAccessLibrary;
using Microsoft.Data.Sqlite;
using Microsoft.QueryStringDotNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using VocabularyReminder.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.System;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace VocabularyReminder
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {

        bool isInBackgroundMode;

        public static int WordId = 0;

        public static MediaPlayer mediaPlayer;
        private Queue<string> listPlayMp3 = new Queue<string>();

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            InitializeDatabase();
            checkMediaPlayer();

            //Mp3.play(mediaPlayer, "");

            this.Suspending += OnSuspending;

            MemoryManager.AppMemoryUsageLimitChanging += MemoryManager_AppMemoryUsageLimitChanging;

            // After an application is backgrounded it is expected to stay
            // under a memory target to maintain priority to keep running.
            // Subscribe to the event that informs the app of this change.
            MemoryManager.AppMemoryUsageIncreased += MemoryManager_AppMemoryUsageIncreased;

            // Subscribe to key lifecyle events to know when the app
            // transitions to and from foreground and background.
            // Leaving the background is an important transition
            // because the app may need to restore UI.
            EnteredBackground += App_EnteredBackground;
            LeavingBackground += App_LeavingBackground;
        }

        public void checkMediaPlayer()
        {
            if (mediaPlayer == null)
            {
                mediaPlayer = new MediaPlayer();
                mediaPlayer.MediaEnded += OnMediaEnded;
            }
        }

        public async static void InitializeDatabase()
        {
            await ApplicationData.Current.LocalFolder.CreateFileAsync("vocabulary.db", CreationCollisionOption.OpenIfExists);
            DataAccess.InitializeDatabase();
        }

        private void App_Suspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            // Optional: Save application state and stop any background activity
            ShowToast("Suspending");
            deferral.Complete();
        }

        /// <summary>
        /// Invoked when application execution is resumed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void App_Resuming(object sender, object e)
        {
            ShowToast("Resuming");
        }

        /// <summary>
        /// Raised when the memory limit for the app is changing, such as when the app
        /// enters the background.
        /// </summary>
        /// <remarks>
        /// If the app is using more than the new limit, it must reduce memory within 2 seconds
        /// on some platforms in order to avoid being suspended or terminated.
        /// 
        /// While some platforms will allow the application
        /// to continue running over the limit, reducing usage in the time
        /// allotted will enable the best experience across the broadest range of devices.
        /// </remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MemoryManager_AppMemoryUsageLimitChanging(object sender, AppMemoryUsageLimitChangingEventArgs e)
        {
            ShowToast("Memory usage limit changing from "
                + (e.OldLimit / 1024) + "K to "
                + (e.NewLimit / 1024) + "K");

            // If app memory usage is over the limit about to be enforced,
            // then reduce usage within 2 seconds to avoid suspending.
            if (MemoryManager.AppMemoryUsage >= e.NewLimit)
            {
                ReduceMemoryUsage(e.NewLimit);
            }
            checkMediaPlayer();
        }

        /// <summary>
        /// Handle system notifications that the app has increased its
        /// memory usage level compared to its current target.
        /// </summary>
        /// <remarks>
        /// The app may have increased its usage or the app may have moved
        /// to the background and the system lowered the target the app
        /// is expected to reach. Either way, if the application wants
        /// to maintain its priority to avoid being suspended before
        /// other apps, it may need to reduce its memory usage.
        /// 
        /// This is not a replacement for handling AppMemoryUsageLimitChanging
        /// which is critical to ensure the app immediately gets below the new
        /// limit. However, once the app is allowed to continue running and
        /// policy is applied, some apps may wish to continue monitoring
        /// usage to ensure they remain below the limit.
        /// </remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MemoryManager_AppMemoryUsageIncreased(object sender, object e)
        {
            ShowToast("Memory usage increased");

            // Obtain the current usage level
            var level = MemoryManager.AppMemoryUsageLevel;

            // Check the usage level to determine whether reducing memory is necessary.
            // Memory usage may have been fine when initially entering the background but
            // a short time later the app might be using more memory and need to trim back.
            if (level == AppMemoryUsageLevel.OverLimit || level == AppMemoryUsageLevel.High)
            {
                ReduceMemoryUsage(MemoryManager.AppMemoryUsageLimit);
            }
        }

        /// <summary>
        /// The application entered the background.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void App_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            // Place the application into "background mode" and note the
            // transition with a flag.
            ShowToast("Entered background");
            isInBackgroundMode = true;

            // An application may wish to release views and view data
            // at this point since the UI is no longer visible.
            //
            // As a performance optimization, here we note instead that 
            // the app has entered background mode with a boolean and
            // defer unloading views until AppMemoryUsageLimitChanging or 
            // AppMemoryUsageIncreased is raised with an indication that
            // the application is under memory pressure.
            checkMediaPlayer();
        }

        /// <summary>
        /// The application is leaving the background.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void App_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            // Mark the transition out of background mode.
            ShowToast("Leaving background");
            isInBackgroundMode = false;

            // Reastore view content if it was previously unloaded.
            if (Window.Current.Content == null)
            {
                ShowToast("Loading view");
                CreateRootFrame(ApplicationExecutionState.Running, string.Empty);
                checkMediaPlayer();
            }
        }

        /// <summary>
        /// Reduces application memory usage.
        /// </summary>
        /// <remarks>
        /// When the app enters the background, receives a memory limit changing
        /// event, or receives a memory usage increased
        /// event it can optionally unload cached data or even its view content in 
        /// order to reduce memory usage and the chance of being suspended.
        /// 
        /// This must be called from multiple event handlers because an application may already
        /// be in a high memory usage state when entering the background or it
        /// may be in a low memory usage state with no need to unload resources yet
        /// and only enter a higher state later.
        /// </remarks>
        public void ReduceMemoryUsage(ulong limit)
        {
            // If the app has caches or other memory it can free, now is the time.
            // << App can release memory here >>

            // Additionally, if the application is currently
            // in background mode and still has a view with content
            // then the view can be released to save memory and 
            // can be recreated again later when leaving the background.
            if (isInBackgroundMode && Window.Current.Content != null)
            {
                ShowToast("Unloading view");

                // Clear the view content. Note that views should rely on
                // events like Page.Unloaded to further release resources. Be careful
                // to also release event handlers in views since references can
                // prevent objects from being collected. C++ developers should take
                // special care to use weak references for event handlers where appropriate.
                Window.Current.Content = null;

                // Finally, clearing the content above and calling GC.Collect() below 
                // is what will trigger each Page.Unloaded handler to be called.
                // In order for the resources each page has allocated to be released,
                // it is necessary that each Page also call GC.Collect() from its
                // Page.Unloaded handler.
            }

            // Run the GC to collect released resources, including triggering
            // each Page.Unloaded handler to run.
            //GC.Collect();
            checkMediaPlayer();
            ShowToast("Finished reducing memory usage");
        }

        /// <summary>
        /// Gets a string describing current memory usage
        /// </summary>
        /// <returns>String describing current memory usage</returns>
        private string GetMemoryUsageText()
        {
            return string.Format("[Memory: Level={0}, Usage={1}K, Target={2}K]",
                MemoryManager.AppMemoryUsageLevel, MemoryManager.AppMemoryUsage / 1024, MemoryManager.AppMemoryUsageLimit / 1024);
        }

        /// <summary>
        /// Sends a toast notification
        /// </summary>
        /// <param name="msg">Message to send</param>
        /// <param name="subMsg">Sub message</param>
        public void ShowToast(string msg, string subMsg = null)
        {
            if (subMsg == null)
                subMsg = GetMemoryUsageText();

            //if (MemoryManager.AppMemoryUsageLevel < AppMemoryUsageLevel.Medium)
            //{
            //    VocabularyToast.reloadLastToast();
            //}

            Debug.WriteLine(msg + "\n" + subMsg);
            return;
        }


        void CreateRootFrame(ApplicationExecutionState previousExecutionState, string arguments)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                System.Diagnostics.Debug.WriteLine("CreateFrame: Initializing root frame ...");

                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                // Set the default language
                rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (previousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                rootFrame.Navigate(typeof(MainPage), arguments);
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                // Ensure the current window is active
                Window.Current.Activate();
            }

            this.RegisterBackgroundTasks();
        }

        protected override void OnActivated(IActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Handle toast activation
            if (e is ToastNotificationActivatedEventArgs)
            {
                var toastActivationArgs = e as ToastNotificationActivatedEventArgs;
                // Parse the query string (using QueryString.NET)
                QueryString args = QueryString.Parse(toastActivationArgs.Argument);

                string main_action;
                args.TryGetValue("action", out main_action);
                // See what action is being requested 
                processCustomAction(main_action, args);

                // If we're loading the app for the first time, place the main page on
                // the back stack so that user can go back after they've been
                // navigated to the specific page
                //if (rootFrame.BackStack.Count == 0)
                //    rootFrame.BackStack.Add(new PageStackEntry(typeof(MainPage), null, null));
            }

            this.RegisterBackgroundTasks();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }

        private void RegisterBackgroundTasks()
        {
            // Actually the background task name registered is 'MyToastNotificationActionTrigger'
            // const string taskName = "ToastBackgroundTask";
            // so it should be like this:
            const string taskName = "MyToastNotificationActionTrigger";

            // If background task is already registered, do nothing
            if (BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name.Equals(taskName)))
                return;

            // Create the background task
            BackgroundTaskBuilder builder = new BackgroundTaskBuilder()
            {
                Name = "MyToastNotificationActionTrigger",
            };

            // Assign the toast action trigger
            builder.SetTrigger(new ToastNotificationActionTrigger());

            // And register the task
            BackgroundTaskRegistration registration = builder.Register();
        }

        
        public void OnMediaEnded(MediaPlayer mediaPlayer, object args)
        {
            while (listPlayMp3.Count() > 0)
            {
                var currentUrl = listPlayMp3.Dequeue();
                Mp3.play(currentUrl);
            }
        }

        private void processCustomAction(string main_action, QueryString args)
        {
            try
            {
                switch (main_action)
                {
                    case "reload":
                        WordId = int.Parse(args["WordId"]);
                        var _item = DataAccess.GetVocabularyById(WordId);
                        VocabularyToast.loadByVocabulary(_item);
                        _item = null;
                        break;
                    case "play":
                        WordId = int.Parse(args["WordId"]);
                        int playId = int.Parse(args["PlayId"]);
                        if (WordId > 0)
                        {
                            string _mp3Url;
                            if (VocabularyToast.reloadLastToast())
                            {
                                _mp3Url = args["PlayUrl"];
                            } else
                            {
                                _item = DataAccess.GetVocabularyById(WordId);
                                VocabularyToast.loadByVocabulary(_item);
                                if (playId == 2)
                                {
                                    _mp3Url = _item.PlayURL2;
                                }
                                else
                                {
                                    _mp3Url = _item.PlayURL;
                                }
                            }

                            if (!String.IsNullOrEmpty(_mp3Url))
                            {
                                if (mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                                {
                                    listPlayMp3.Enqueue(_mp3Url);
                                } else
                                {
                                    checkMediaPlayer();
                                    Mp3.play(_mp3Url);
                                }
                            }
                        }
                        break;
                    case "next":
                        WordId = int.Parse(args["WordId"]);
                        if (WordId > 0)
                        {
                            WordId++;
                        } else {
                            WordId = DataAccess.GetFirstWordId();
                        }
                        var _item2 = DataAccess.GetVocabularyById(WordId);
                        VocabularyToast.loadByVocabulary(_item2);
                        _item2 = null;
                        break;
                    case "view":
                        string SearchUrl = args["url"];
                        // The URI to launch
                        var uriBing = new Uri(SearchUrl);
                        // Launch the URI
                        var success = Windows.System.Launcher.LaunchUriAsync(uriBing);
                        break;
                }
            } catch (Exception ex)
            {
                Helper.ShowToast("Action Background Error: " + ex.Message);
            }
        }

        protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            var deferral = args.TaskInstance.GetDeferral();

            if (args.TaskInstance.TriggerDetails is ToastNotificationActionTriggerDetail)
            {
                var toastArgs = args.TaskInstance.TriggerDetails as ToastNotificationActionTriggerDetail;

                QueryString qs = QueryString.Parse(toastArgs.Argument);

                if (qs.TryGetValue("action", out string action))
                {
                    processCustomAction(action, qs);
                }
            }

            deferral.Complete();
        }

    }

}
