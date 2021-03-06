﻿using Microsoft.QueryStringDotNET;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using DataAccessLibrary;
using VocabularyReminder.Services;
using System.Diagnostics;

namespace VocabularyReminder
{
    public class VocabularyToast
    {
        const string viewDicOnlineUrl = "https://www.oxfordlearnersdictionaries.com/definition/english/";
        public static void loadByVocabulary(Vocabulary _item)
        {
            if (_item.Id == 0)
            {
                Helper.ShowToast("Chưa có dữ liệu từ điển. Vui lòng import.");
                return;
            }
            ToastContent content;
            if (String.IsNullOrEmpty(_item.PlayURL))
            {
                content = getToastContentWithoutPlay(_item);
            } else
            {
                Mp3.preloadMp3FileSingle(_item);
                content = getToastContent(_item);
            }
            
            var _toastItem = new ToastNotification(content.GetXml())
            {
                Tag = "Vocabulary",
                Group = "Reminder",
            };

            _toastItem.Dismissed += ToastDismissed;
            _toastItem.Failed += ToastFailed;
            _toastItem.Priority = ToastNotificationPriority.High;

            ToastNotificationManager.CreateToastNotifier().Show(_toastItem);
        }

        public static bool reloadLastToast()
        {
            var _history = ToastNotificationManager.History.GetHistory();
            if (_history.Count() > 0) {
                ToastNotificationManager.CreateToastNotifier().Show(_history.Last());
                return true;
            }
            return false;
        }

        private static void ToastDismissed(object source, ToastDismissedEventArgs e)
        {
            switch (e.Reason)
            {
                case ToastDismissalReason.ApplicationHidden:
                    // Application hid the toast with ToastNotifier.Hide
                    Debug.WriteLine("Application Hidden");
                    break;
                case ToastDismissalReason.UserCanceled:
                    Debug.WriteLine("User dismissed the toast");
                    break;
                case ToastDismissalReason.TimedOut:
                    Debug.WriteLine("Toast timeout elapsed");
                    break;
            }

            Debug.WriteLine("Toast Dismissed: " + e.Reason.ToString());
        }

        private static void ToastFailed(object source, ToastFailedEventArgs e)
        {
            // Check the error code
            var errorCode = e.ErrorCode;
            Debug.WriteLine("Error code:{0}", errorCode);
        }

        private static ToastContent getToastContent(Vocabulary _item)
        {
            string _Ipa = _item.Ipa;
            if (_item.Ipa != _item.Ipa2) {
                _Ipa = _item.Ipa + " " + _item.Ipa2;
            }
            
            ToastContent content = new ToastContent()
            {

                Duration = ToastDuration.Long,
                Launch = "vocabulary-reminder",
                Audio = new ToastAudio() { Silent = true },
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Attribution = new ToastGenericAttributionText()
                        {
                            Text = _item.Type
                        },
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = _item.Define,
                            },

                            new AdaptiveText()
                            {
                                Text = _item.Example,
                            },

                            new AdaptiveText()
                            {
                                Text = _item.Example2,
                            },

                            new AdaptiveGroup()
                            {
                                Children =
                                {
                                    new AdaptiveSubgroup()
                                    {
                                        Children =
                                        {
                                            new AdaptiveText()
                                            { 
                                                Text = _item.Word + " " + _Ipa,
                                                HintStyle = AdaptiveTextStyle.Subtitle,
                                            },
                                            new AdaptiveText()
                                            {
                                                Text = _item.Translate,
                                                HintStyle = AdaptiveTextStyle.Base,
                                            },
                                            new AdaptiveText()
                                            {
                                                Text = _item.Related,
                                                HintStyle = AdaptiveTextStyle.CaptionSubtle
                                            }
                                        }
                                    },
                                }
                            }
                        },
                        HeroImage = new ToastGenericHeroImage()
                        {
                            Source = "https://picsum.photos/364/180?image=1043"
                        },

                    }
                },
                Scenario = ToastScenario.Reminder,
                Actions = new ToastActionsCustom()
                {
                    ContextMenuItems =
                    {
                        new ToastContextMenuItem("Reload", "action=reload&WordId=" + _item.Id.ToString())
                    },
                    Buttons =
                        {
                            new ToastButton("\u25B6", new QueryString()
                            {
                                { "action", "play" },
                                { "WordId", _item.Id.ToString() },
                                { "PlayId", "1" },
                                { "PlayUrl", _item.PlayURL },
                            }.ToString()) {
                                ActivationType = ToastActivationType.Background,
                                ActivationOptions = new ToastActivationOptions()
                                {
                                    AfterActivationBehavior = ToastAfterActivationBehavior.PendingUpdate
                                }
                            },
                            new ToastButton("\u25B7", new QueryString()
                            {
                                { "action", "play" },
                                { "WordId", _item.Id.ToString() },
                                { "PlayId", "2" },
                                { "PlayUrl", _item.PlayURL2 },
                            }.ToString())
                            {
                                ActivationType = ToastActivationType.Background,
                                ActivationOptions = new ToastActivationOptions()
                                {
                                    AfterActivationBehavior = ToastAfterActivationBehavior.PendingUpdate
                                }
                            },
                            new ToastButton("Next", new QueryString()
                            {
                                { "action", "next" },
                                { "WordId", _item.Id.ToString() },
                            }.ToString())
                            {
                                ActivationType = ToastActivationType.Background,
                                ActivationOptions = new ToastActivationOptions()
                                {
                                    AfterActivationBehavior = ToastAfterActivationBehavior.PendingUpdate
                                }
                            },
                            new ToastButton("View", new QueryString()
                            {
                                { "action", "view" },
                                { "url", viewDicOnlineUrl + _item.Word }
                             }.ToString()),
                            //new ToastButton("Close", "dismiss")
                            //{
                            //    ActivationType = ToastActivationType.Background
                            //},
                        }
                },

            };

            return content;
        }

        private static ToastContent getToastContentWithoutPlay(Vocabulary _item)
        {
            ToastContent content = new ToastContent()
            {
                Launch = "vocabulary-reminder",
                Audio = new ToastAudio() { Silent = true },
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = _item.Word,
                                HintMaxLines = 1
                            },

                            new AdaptiveText()
                            {
                                Text = _item.Ipa,
                            },

                            new AdaptiveText()
                            {
                                Text = _item.Translate
                            }
                        },
                        HeroImage = new ToastGenericHeroImage()
                        {
                            Source = "https://picsum.photos/364/180?image=1043"
                        },

                    }
                },
                Scenario = ToastScenario.Reminder,
                Actions = new ToastActionsCustom()
                {
                    Buttons =
                        {
                            new ToastButton("Next", new QueryString()
                            {
                                { "WordId", _item.Id.ToString() },
                                { "action", "next" },
                            }.ToString())
                            {
                                ActivationType = ToastActivationType.Background,
                                ActivationOptions = new ToastActivationOptions()
                                {
                                    AfterActivationBehavior = ToastAfterActivationBehavior.PendingUpdate
                                }
                            },
                            new ToastButton("View", new QueryString()
                                {
                                    { "action", "view" },
                                    { "url", viewDicOnlineUrl + _item.Word }

                                }.ToString()),
                            new ToastButton("Skip", "dismiss")
                            {
                                ActivationType = ToastActivationType.Background
                            },
                        }
                },

            };

            return content;
        }
    }

}
