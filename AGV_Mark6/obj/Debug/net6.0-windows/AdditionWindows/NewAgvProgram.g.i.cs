﻿#pragma checksum "..\..\..\..\AdditionWindows\NewAgvProgram.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "582ECF1AA3CE683DFFFEC88B5020A988CC1699AF"
//------------------------------------------------------------------------------
// <auto-generated>
//     Этот код создан программой.
//     Исполняемая версия:4.0.30319.42000
//
//     Изменения в этом файле могут привести к неправильной работе и будут потеряны в случае
//     повторной генерации кода.
// </auto-generated>
//------------------------------------------------------------------------------

using AGV_Mark6;
using AGV_Mark6.Model;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace AGV_Mark6 {
    
    
    /// <summary>
    /// NewAgvProgram
    /// </summary>
    public partial class NewAgvProgram : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        
        #line 156 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Expander EX_NotePad;
        
        #line default
        #line hidden
        
        
        #line 209 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.TextBox TB_ProgramName;
        
        #line default
        #line hidden
        
        
        #line 220 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Grid Grid_1;
        
        #line default
        #line hidden
        
        
        #line 221 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.DataGrid DG_Steps1;
        
        #line default
        #line hidden
        
        
        #line 458 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button BT_SaveProgram;
        
        #line default
        #line hidden
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "8.0.0.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/AGV_Mark6;V1.0.0.0;component/additionwindows/newagvprogram.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "8.0.0.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            switch (connectionId)
            {
            case 1:
            
            #line 8 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
            ((AGV_Mark6.NewAgvProgram)(target)).Closed += new System.EventHandler(this.Window_Closed);
            
            #line default
            #line hidden
            return;
            case 2:
            this.EX_NotePad = ((System.Windows.Controls.Expander)(target));
            
            #line 160 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
            this.EX_NotePad.Expanded += new System.Windows.RoutedEventHandler(this.Expander_Expanded);
            
            #line default
            #line hidden
            
            #line 161 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
            this.EX_NotePad.Collapsed += new System.Windows.RoutedEventHandler(this.Expander_Collapsed);
            
            #line default
            #line hidden
            return;
            case 3:
            this.TB_ProgramName = ((System.Windows.Controls.TextBox)(target));
            
            #line 209 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
            this.TB_ProgramName.PreviewTextInput += new System.Windows.Input.TextCompositionEventHandler(this.TB_ProgramName_PreviewTextInput);
            
            #line default
            #line hidden
            
            #line 209 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
            this.TB_ProgramName.TextChanged += new System.Windows.Controls.TextChangedEventHandler(this.TB_ProgramName_TextChanged);
            
            #line default
            #line hidden
            return;
            case 4:
            this.Grid_1 = ((System.Windows.Controls.Grid)(target));
            return;
            case 5:
            this.DG_Steps1 = ((System.Windows.Controls.DataGrid)(target));
            
            #line 223 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
            this.DG_Steps1.PreviewTextInput += new System.Windows.Input.TextCompositionEventHandler(this.DG_Steps1_PreviewTextInput);
            
            #line default
            #line hidden
            
            #line 224 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
            this.DG_Steps1.CurrentCellChanged += new System.EventHandler<System.EventArgs>(this.DG_Steps1_CurrentCellChanged);
            
            #line default
            #line hidden
            
            #line 225 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
            this.DG_Steps1.PreviewKeyDown += new System.Windows.Input.KeyEventHandler(this.DG_Steps1_PreviewKeyDown);
            
            #line default
            #line hidden
            
            #line 226 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
            this.DG_Steps1.PreviewKeyUp += new System.Windows.Input.KeyEventHandler(this.DG_Steps1_PreviewKeyUp);
            
            #line default
            #line hidden
            return;
            case 6:
            this.BT_SaveProgram = ((System.Windows.Controls.Button)(target));
            
            #line 462 "..\..\..\..\AdditionWindows\NewAgvProgram.xaml"
            this.BT_SaveProgram.Click += new System.Windows.RoutedEventHandler(this.BT_SaveProgram_Click);
            
            #line default
            #line hidden
            return;
            }
            this._contentLoaded = true;
        }
    }
}

