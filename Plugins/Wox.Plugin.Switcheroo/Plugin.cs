using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Windows.Forms;
using ManagedWinapi;
using ManagedWinapi.Windows;
using Switcheroo;
using Switcheroo.Core;
using Wox.Infrastructure;
using Wox.Infrastructure.Hotkey;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Control = System.Windows.Controls.Control;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Wox.Plugin.Switcheroo
{
    public class Plugin : IPlugin, ISettingProvider, IContextMenu
    {
        private bool _altTabHooked;
        private SwitcherooSettings _settings;
        private PluginJsonStorage<SwitcherooSettings> _storage;
        private IntPtr _woxWindowHandle;
        protected PluginInitContext Context;

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var app = (AppWindow) selectedResult.ContextData;
            return new List<Result>
            {
                new Result
                {
                    Title = "Close " + app.Title,
                    IcoPath = app.ExecutablePath,
                    Action = e =>
                    {
                        Context.API.ChangeQuery(Context.CurrentPluginMetadata.ActionKeyword + " ", true);
                        app.PostClose();
                        return true;
                    }
                }
            };
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
            _storage = new PluginJsonStorage<SwitcherooSettings>();
            _settings = _storage.Load();
            context.API.GlobalKeyboardEvent += API_GlobalKeyboardEvent;
        }

        public List<Result> Query(Query query)
        {

            var queryString = query.Search;

            var windowContext = new WindowFilterContext<AppWindowViewModel>
            {
                Windows = new WindowFinder().GetWindows().Select(w => new AppWindowViewModel(w)),
                ForegroundWindowProcessTitle = new AppWindow(SystemWindow.ForegroundWindow.HWnd).ProcessTitle
            };
            var filterResults = windowContext.Windows
                .Where(r =>
                {
                    return PathAnalysis.isMatch(r.ProcessTitle+" "+r.WindowTitle, queryString);
                })
                .OrderByDescending(r => -r.ProcessTitle.Length)
                .ToList();

//            var filterResults =
//                new WindowFilterer().Filter(windowContext, queryString).Select(o => o.AppWindow.AppWindow)
//                    .Where(window => window.ProcessTitle != "Wox")
//                    .ToList();


            var results = filterResults.Select(o =>
            {
                var result = new Result
                {
                    Title = o.ProcessTitle,
                    SubTitle = o.WindowTitle,
                    IcoPath = o.AppWindow.ExecutablePath,
                    ContextData = o,
                    Action = con =>
                    {
                        o.AppWindow.SwitchTo();
                        Context.API.HideApp();
                        return true;
                    }
                };
                return result;
            }).ToList();

            return results;
        }

        public Control CreateSettingPanel()
        {
            return new SwitcherooSetting(_settings, _storage);
        }

        private void ActivateWindow()
        {
            var altKey = new KeyboardKey(Keys.Alt);
            var altKeyPressed = false;

            if ((altKey.AsyncState & 0x8000) == 0)
            {
                altKey.Press();
                altKeyPressed = true;
            }

            Context.API.ShowApp();

            if (altKeyPressed)
            {
                altKey.Release();
            }
        }

        private bool API_GlobalKeyboardEvent(int keyevent, int vkcode, SpecialKeyState state)
        {
            if (!_settings.OverrideAltTab) return true;
            if (keyevent == (int) KeyEvent.WM_SYSKEYDOWN && vkcode == (int) Keys.Tab && state.AltPressed)
            {
                OnAltTabPressed();
                return false;
            }

            if (keyevent == (int) KeyEvent.WM_SYSKEYUP && vkcode == (int) Keys.Tab)
            {
                //prevent system alt+tab action
                return false;
            }

            return true;
        }

        private void OnAltTabPressed()
        {
            Context.API.ChangeQuery(" ", true);
            Context.API.ChangeQuery(Context.CurrentPluginMetadata.ActionKeyword + " ", true);
            ActivateWindow();
        }

        private string GetNormalTitleOrAppFirst(string title)
        {
            if (!_settings.ApplicationNameFirst)
                return title;
            var lastIndexOfDash = title.LastIndexOf('-');
            if (lastIndexOfDash == -1)
                return title;
            var appName = title.Substring(lastIndexOfDash + 2);
            var restOfTitle = title.Substring(0, title.LastIndexOf('-'));
            return String.Format("{0} - {1}", appName, restOfTitle);
        }
    }
}