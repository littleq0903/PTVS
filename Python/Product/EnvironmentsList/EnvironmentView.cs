// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.EnvironmentsList {
    public sealed class EnvironmentView : DependencyObject {
        public static readonly RoutedCommand OpenInteractiveWindow = new RoutedCommand();
        public static readonly RoutedCommand OpenInteractiveScripts = new RoutedCommand();
        public static readonly RoutedCommand OpenInPowerShell = new RoutedCommand();
        public static readonly RoutedCommand OpenInCommandPrompt = new RoutedCommand();
        public static readonly RoutedCommand MakeGlobalDefault = new RoutedCommand();
        public static readonly RoutedCommand MakeActiveInCurrentProject = new RoutedCommand();

        public static readonly RoutedCommand EnableIPythonInteractive = new RoutedCommand();
        public static readonly RoutedCommand DisableIPythonInteractive = new RoutedCommand();

        public static readonly Lazy<EnvironmentView> AddNewEnvironmentView =
            new Lazy<EnvironmentView>(() => new EnvironmentView());
        public static readonly Lazy<IEnumerable<EnvironmentView>> AddNewEnvironmentViewOnce =
            new Lazy<IEnumerable<EnvironmentView>>(() => new[] { AddNewEnvironmentView.Value });

        public static readonly Lazy<EnvironmentView> OnlineHelpView =
            new Lazy<EnvironmentView>(() => new EnvironmentView());
        public static readonly Lazy<IEnumerable<EnvironmentView>> OnlineHelpViewOnce =
            new Lazy<IEnumerable<EnvironmentView>>(() => new[] { OnlineHelpView.Value });

        // Names of properties that will be requested from interpreter configurations
        internal const string VendorKey = "Vendor";
        internal const string SupportUrlKey = "SupportUrl";

        /// <summary>
        /// Used with <see cref="CommonUtils.FindFile"/> to more efficiently
        /// find interpreter executables.
        /// </summary>
        private static readonly string[] _likelyInterpreterPaths = new[] { "Scripts" };

        /// <summary>
        /// Used with <see cref="CommonUtils.FindFile"/> to more efficiently
        /// find interpreter libraries.
        /// </summary>
        private static readonly string[] _likelyLibraryPaths = new[] { "Lib" };

        private readonly IInterpreterOptionsService _service;
        private readonly IInterpreterRegistryService _registry;
        private readonly IPythonInterpreterFactoryWithDatabase _withDb;

        public IPythonInterpreterFactory Factory { get; private set; }

        private EnvironmentView() { }

        internal EnvironmentView(
            IInterpreterOptionsService service,
            IInterpreterRegistryService registry,
            IPythonInterpreterFactory factory,
            Redirector redirector
        ) {
            if (service == null) {
                throw new ArgumentNullException(nameof(service));
            }
            if (registry == null) {
                throw new ArgumentNullException(nameof(registry));
            }
            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }

            _service = service;
            _registry = registry;
            Factory = factory;

            _withDb = factory as IPythonInterpreterFactoryWithDatabase;
            if (_withDb != null) {
                _withDb.IsCurrentChanged += Factory_IsCurrentChanged;
                IsCheckingDatabase = _withDb.IsCheckingDatabase;
                IsCurrent = _withDb.IsCurrent;
            }
            

            if (_service.IsConfigurable(factory.Configuration.Id)) {
                IsConfigurable = true;
            }

            Description = Factory.Configuration.FullDescription;
            IsDefault = (_service != null && _service.DefaultInterpreter == Factory);

            PrefixPath = Factory.Configuration.PrefixPath;
            InterpreterPath = Factory.Configuration.InterpreterPath;
            WindowsInterpreterPath = Factory.Configuration.WindowsInterpreterPath;
            LibraryPath = Factory.Configuration.LibraryPath;

            Extensions = new ObservableCollection<object>();
            Extensions.Add(new EnvironmentPathsExtensionProvider());
            if (IsConfigurable) {
                Extensions.Add(new ConfigurationExtensionProvider(_service));
            }

            CanBeDefault = Factory.CanBeDefault();

            Vendor = _registry.GetProperty(Factory.Configuration.Id, "Vendor") as string;
            SupportUrl = _registry.GetProperty(Factory.Configuration.Id, "SupportUrl") as string;
        }

        public override string ToString() {
            return string.Format(
                "{{{0}:{1}}}", GetType().FullName,
                _withDb == null ? "(null)" : _withDb.Configuration.FullDescription
            );
        }

        public ObservableCollection<object> Extensions { get; private set; }

        private void Factory_IsCurrentChanged(object sender, EventArgs e) {
            Debug.Assert(_withDb != null);
            if (_withDb == null) {
                return;
            }

            Dispatcher.BeginInvoke((Action)(() => {
                IsCheckingDatabase = _withDb.IsCheckingDatabase;
                IsCurrent = _withDb.IsCurrent;
            }));
        }

        #region Read-only State Dependency Properties

        private static readonly DependencyPropertyKey IsConfigurablePropertyKey = DependencyProperty.RegisterReadOnly("IsConfigurable", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey CanBeDefaultPropertyKey = DependencyProperty.RegisterReadOnly("CanBeDefault", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(true));
        private static readonly DependencyPropertyKey IsDefaultPropertyKey = DependencyProperty.RegisterReadOnly("IsDefault", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey IsCurrentPropertyKey = DependencyProperty.RegisterReadOnly("IsCurrent", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(true));
        private static readonly DependencyPropertyKey IsCheckingDatabasePropertyKey = DependencyProperty.RegisterReadOnly("IsCheckingDatabase", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey RefreshDBProgressPropertyKey = DependencyProperty.RegisterReadOnly("RefreshDBProgress", typeof(int), typeof(EnvironmentView), new PropertyMetadata(0));
        private static readonly DependencyPropertyKey RefreshDBMessagePropertyKey = DependencyProperty.RegisterReadOnly("RefreshDBMessage", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey IsRefreshingDBPropertyKey = DependencyProperty.RegisterReadOnly("IsRefreshingDB", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey IsRefreshDBProgressIndeterminatePropertyKey = DependencyProperty.RegisterReadOnly("IsRefreshDBProgressIndeterminate", typeof(bool), typeof(EnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty IsConfigurableProperty = IsConfigurablePropertyKey.DependencyProperty;
        public static readonly DependencyProperty CanBeDefaultProperty = CanBeDefaultPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsDefaultProperty = IsDefaultPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsCurrentProperty = IsCurrentPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsCheckingDatabaseProperty = IsCheckingDatabasePropertyKey.DependencyProperty;
        public static readonly DependencyProperty RefreshDBMessageProperty = RefreshDBMessagePropertyKey.DependencyProperty;
        public static readonly DependencyProperty RefreshDBProgressProperty = RefreshDBProgressPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsRefreshingDBProperty = IsRefreshingDBPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsRefreshDBProgressIndeterminateProperty = IsRefreshDBProgressIndeterminatePropertyKey.DependencyProperty;

        public bool IsConfigurable {
            get { return Factory == null ? false : (bool)GetValue(IsConfigurableProperty); }
            set { if (Factory != null) { SetValue(IsConfigurablePropertyKey, value); } }
        }

        public bool CanBeDefault {
            get { return Factory == null ? false : (bool)GetValue(CanBeDefaultProperty); }
            set { if (Factory != null) { SetValue(CanBeDefaultPropertyKey, value); } }
        }

        public bool IsDefault {
            get { return Factory == null ? false : (bool)GetValue(IsDefaultProperty); }
            internal set { if (Factory != null) { SetValue(IsDefaultPropertyKey, value); } }
        }

        public bool IsCurrent {
            get { return Factory == null ? true : (bool)GetValue(IsCurrentProperty); }
            internal set { if (Factory != null) { SetValue(IsCurrentPropertyKey, value); } }
        }

        public bool IsCheckingDatabase {
            get { return Factory == null ? false : (bool)GetValue(IsCheckingDatabaseProperty); }
            internal set { if (Factory != null) { SetValue(IsCheckingDatabasePropertyKey, value); } }
        }

        public int RefreshDBProgress {
            get { return Factory == null ? 0 : (int)GetValue(RefreshDBProgressProperty); }
            internal set { if (Factory != null) { SetValue(RefreshDBProgressPropertyKey, value); } }
        }

        public string RefreshDBMessage {
            get { return Factory == null ? string.Empty : (string)GetValue(RefreshDBMessageProperty); }
            internal set { if (Factory != null) { SetValue(RefreshDBMessagePropertyKey, value); } }
        }

        public bool IsRefreshingDB {
            get { return Factory == null ? false : (bool)GetValue(IsRefreshingDBProperty); }
            internal set { if (Factory != null) { SetValue(IsRefreshingDBPropertyKey, value); } }
        }

        public bool IsRefreshDBProgressIndeterminate {
            get { return Factory == null ? false : (bool)GetValue(IsRefreshDBProgressIndeterminateProperty); }
            internal set { if (Factory != null) { SetValue(IsRefreshDBProgressIndeterminatePropertyKey, value); } }
        }

        #endregion

        #region Configuration Dependency Properties

        private static readonly DependencyPropertyKey DescriptionPropertyKey = DependencyProperty.RegisterReadOnly("Description", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey PrefixPathPropertyKey = DependencyProperty.RegisterReadOnly("PrefixPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey InterpreterPathPropertyKey = DependencyProperty.RegisterReadOnly("InterpreterPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey WindowsInterpreterPathPropertyKey = DependencyProperty.RegisterReadOnly("WindowsInterpreterPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey LibraryPathPropertyKey = DependencyProperty.RegisterReadOnly("LibraryPath", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey PathEnvironmentVariablePropertyKey = DependencyProperty.RegisterReadOnly("PathEnvironmentVariable", typeof(string), typeof(EnvironmentView), new PropertyMetadata());

        public static readonly DependencyProperty DescriptionProperty = DescriptionPropertyKey.DependencyProperty;
        public static readonly DependencyProperty PrefixPathProperty = PrefixPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty InterpreterPathProperty = InterpreterPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty WindowsInterpreterPathProperty = WindowsInterpreterPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty LibraryPathProperty = LibraryPathPropertyKey.DependencyProperty;
        public static readonly DependencyProperty PathEnvironmentVariableProperty = PathEnvironmentVariablePropertyKey.DependencyProperty;

        public string Description {
            get { return Factory == null ? string.Empty : (string)GetValue(DescriptionProperty); }
            set { if (Factory != null) { SetValue(DescriptionPropertyKey, value); } }
        }

        public string PrefixPath {
            get { return Factory == null ? string.Empty : (string)GetValue(PrefixPathProperty); }
            set { if (Factory != null) { SetValue(PrefixPathPropertyKey, value); } }
        }

        public string InterpreterPath {
            get { return Factory == null ? string.Empty : (string)GetValue(InterpreterPathProperty); }
            set { if (Factory != null) { SetValue(InterpreterPathPropertyKey, value); } }
        }

        public string WindowsInterpreterPath {
            get { return Factory == null ? string.Empty : (string)GetValue(WindowsInterpreterPathProperty); }
            set { if (Factory != null) { SetValue(WindowsInterpreterPathPropertyKey, value); } }
        }

        public string LibraryPath {
            get { return Factory == null ? string.Empty : (string)GetValue(LibraryPathProperty); }
            set { if (Factory != null) { SetValue(LibraryPathPropertyKey, value); } }
        }

        public string PathEnvironmentVariable {
            get { return Factory == null ? string.Empty : (string)GetValue(PathEnvironmentVariableProperty); }
            set { if (Factory != null) { SetValue(PathEnvironmentVariablePropertyKey, value); } }
        }

        #endregion

        #region Extra Information Dependency Properties

        private static readonly DependencyPropertyKey VendorPropertyKey = DependencyProperty.RegisterReadOnly("Vendor", typeof(string), typeof(EnvironmentView), new PropertyMetadata());
        private static readonly DependencyPropertyKey SupportUrlPropertyKey = DependencyProperty.RegisterReadOnly("SupportUrl", typeof(string), typeof(EnvironmentView), new PropertyMetadata());

        public static readonly DependencyProperty VendorProperty = VendorPropertyKey.DependencyProperty;
        public static readonly DependencyProperty SupportUrlProperty = SupportUrlPropertyKey.DependencyProperty;

        public string Vendor {
            get { return Factory == null ? string.Empty : (string)GetValue(VendorProperty); }
            set { if (Factory != null) { SetValue(VendorPropertyKey, value); } }
        }

        public string SupportUrl {
            get { return Factory == null ? string.Empty : (string)GetValue(SupportUrlProperty); }
            set { if (Factory != null) { SetValue(SupportUrlPropertyKey, value); } }
        }

        #endregion
    }

    public sealed class EnvironmentViewTemplateSelector : DataTemplateSelector {
        public DataTemplate Environment { get; set; }

        public DataTemplate AddNewEnvironment { get; set; }

        public DataTemplate OnlineHelp { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            if (EnvironmentView.AddNewEnvironmentView.IsValueCreated) {
                if (object.ReferenceEquals(item, EnvironmentView.AddNewEnvironmentView.Value) &&
                    AddNewEnvironment != null) {
                    return AddNewEnvironment;
                }
            }
            if (EnvironmentView.OnlineHelpView.IsValueCreated) {
                if (object.ReferenceEquals(item, EnvironmentView.OnlineHelpView.Value) &&
                    OnlineHelp != null) {
                    return OnlineHelp;
                }
            }
            if (item is EnvironmentView && Environment != null) {
                return Environment;
            }
            return base.SelectTemplate(item, container);
        }
    }
}
