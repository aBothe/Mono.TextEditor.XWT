//
// TextEditor.cs
//
// Author:
//   Mike Krüger <mkrueger@novell.com>
//
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

//#define DEBUG_EXPOSE

using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Mono.TextEditor.Highlighting;
using Mono.TextEditor.PopupWindow;
using Mono.TextEditor.Theatrics;

using Xwt;
using Xwt.Drawing;
using System.Timers;

namespace Mono.TextEditor
{
	public class TextArea : Canvas, ITextEditorDataProvider
	{
		TextEditorData textEditorData;
		
		protected IconMargin       iconMargin;
		protected GutterMargin     gutterMargin;
//		protected DashedLineMargin dashedLineMargin;
		protected FoldMarkerMargin foldMarkerMargin;
		protected TextViewMargin   textViewMargin;
		
		DocumentLine longestLine      = null;
		double      longestLineWidth = -1;
		
		List<Margin> margins = new List<Margin> ();
		int oldRequest = -1;
		
		bool isDisposed = false;
		/*IMMulticontext imContext;
		Gdk.EventKey lastIMEvent;
		*/Key lastIMEventMappedKey;
		uint lastIMEventMappedChar;
		ModifierKeys lastIMEventMappedModifier;
		bool sizeHasBeenAllocated;
		bool imContextNeedsReset;
		string currentStyleName;
		
		double mx, my;
		
		public TextDocument Document {
			get {
				return textEditorData.Document;
			}
		}

		public bool IsDisposed {
			get {
				return textEditorData.IsDisposed;
			}
		}
		
		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="Mono.TextEditor.TextEditor"/> converts tabs to spaces.
		/// It is possible to overwrite the default options value for certain languages (like F#).
		/// </summary>
		/// <value>
		/// <c>true</c> if tabs to spaces should be converted; otherwise, <c>false</c>.
		/// </value>
		public bool TabsToSpaces {
			get {
				return textEditorData.TabsToSpaces;
			}
			set {
				textEditorData.TabsToSpaces = value;
			}
		}
		
		public Mono.TextEditor.Caret Caret {
			get {
				return textEditorData.Caret;
			}
		}
		/*
		protected internal IMMulticontext IMContext {
			get { return imContext; }
		}*/

		public MenuItem CreateInputMethodMenuItem (string label)
		{/*
			if (GtkWorkarounds.GtkMinorVersion >= 16) {
				bool showMenu = (bool) GtkWorkarounds.GetProperty (Settings, "gtk-show-input-method-menu").Val;
				if (!showMenu)
					return null;
			}*/
			MenuItem imContextMenuItem = new MenuItem (label);
			Menu imContextMenu = new Menu ();
			imContextMenuItem.SubMenu = imContextMenu;
			//IMContext.AppendMenuitems (imContextMenu);
			return imContextMenuItem;
		}
		/*
		[DllImport (PangoUtil.LIBGTK, CallingConvention = CallingConvention.Cdecl)]
		static extern void gtk_im_multicontext_set_context_id (IntPtr context, string context_id);

		[DllImport (PangoUtil.LIBGTK, CallingConvention = CallingConvention.Cdecl)]
		static extern string gtk_im_multicontext_get_context_id (IntPtr context);
		
		[GLib.Property ("im-module")]
		public string IMModule {
			get {
				if (GtkWorkarounds.GtkMinorVersion < 16 || imContext == null)
					return null;
				return gtk_im_multicontext_get_context_id (imContext.Handle);
			}
			set {
				if (GtkWorkarounds.GtkMinorVersion < 16 || imContext == null)
					return;
				gtk_im_multicontext_set_context_id (imContext.Handle, value);
			}
		}*/
		
		public ITextEditorOptions Options {
			get {
				return textEditorData.Options;
			}
			set {
				if (textEditorData.Options != null)
					textEditorData.Options.Changed -= OptionsChanged;
				textEditorData.Options = value;
				if (textEditorData.Options != null) {
					textEditorData.Options.Changed += OptionsChanged;
					OptionsChanged (null, null);
				}
			}
		}
		
		
		public string FileName {
			get {
				return Document.FileName;
			}
		}
		
		public string MimeType {
			get {
				return Document.MimeType;
			}
		}

		void HandleTextEditorDataDocumentMarkerChange (object sender, TextMarkerEvent e)
		{
			if (e.TextMarker is IExtendingTextLineMarker) {
				int lineNumber = e.Line.LineNumber;
				if (lineNumber <= LineCount) {
					try {
						textEditorData.HeightTree.SetLineHeight (lineNumber, GetLineHeight (e.Line));
					} catch (Exception ex) {
						Console.WriteLine (ex);
					}
				}
			}
		}
		
		void HAdjustmentValueChanged (object sender, EventArgs args)
		{/*
			var alloc = this.Allocation;
			alloc.X = alloc.Y = 0;
			*/
			HAdjustmentValueChanged ();
		}
		
		protected virtual void HAdjustmentValueChanged ()
		{
			HideTooltip (false);
			double value = this.textEditorData.HAdjustment.Value;
			if (value != System.Math.Round (value)) {
				this.textEditorData.HAdjustment.Value = System.Math.Round (value);
				return;
			}
			textViewMargin.HideCodeSegmentPreviewWindow ();
			QueueDrawArea ((int)this.textViewMargin.XOffset, 0, base.WidthRequest - (int)this.textViewMargin.XOffset, HeightRequest);
			OnHScroll (EventArgs.Empty);
			SetChildrenPositions (Bounds);
		}
		
		void VAdjustmentValueChanged (object sender, EventArgs args)
		{/*
			var alloc = this.Allocation;
			alloc.X = alloc.Y = 0;
			*/
			VAdjustmentValueChanged ();
			SetChildrenPositions (Bounds);
		}
		
		protected virtual void VAdjustmentValueChanged ()
		{
			HideTooltip (false);
			textViewMargin.HideCodeSegmentPreviewWindow ();
			double value = this.textEditorData.VAdjustment.Value;
			if (value != System.Math.Round (value)) {
				this.textEditorData.VAdjustment.Value = System.Math.Round (value);
				return;
			}
			if (isMouseTrapped)
				FireMotionEvent (mx + textViewMargin.XOffset, my, lastModifierKeys);
			
			double delta = value - this.oldVadjustment;
			oldVadjustment = value;
			TextViewMargin.caretY -= delta;
			
			if (System.Math.Abs (delta) >= Size.Height - this.LineHeight * 2 || this.TextViewMargin.InSelectionDrag) {
				this.QueueDraw ();
				OnVScroll (EventArgs.Empty);
				return;
			}

			/*TODO
			if (GdkWindow != null)
				GdkWindow.Scroll (0, (int)-delta);
			*/
			OnVScroll (EventArgs.Empty);
		}
		
		protected virtual void OnVScroll (EventArgs e)
		{
			EventHandler handler = this.VScroll;
			if (handler != null)
				handler (this, e);
		}

		protected virtual void OnHScroll (EventArgs e)
		{
			EventHandler handler = this.HScroll;
			if (handler != null)
				handler (this, e);
		}
		
		public event EventHandler VScroll;
		public event EventHandler HScroll;

		void UnregisterAdjustments ()
		{
			if (textEditorData.HAdjustment != null)
				textEditorData.HAdjustment.ValueChanged -= HAdjustmentValueChanged;
			if (textEditorData.VAdjustment != null)
				textEditorData.VAdjustment.ValueChanged -= VAdjustmentValueChanged;
		}

		internal void SetTextEditorScrollAdjustments (ScrollAdjustment hAdjustement, ScrollAdjustment vAdjustement)
		{
			if (textEditorData == null)
				return;
			UnregisterAdjustments ();
			
			if (hAdjustement == null || vAdjustement == null)
				return;

			this.textEditorData.HAdjustment = hAdjustement;
			this.textEditorData.VAdjustment = vAdjustement;
			
			this.textEditorData.HAdjustment.ValueChanged += HAdjustmentValueChanged;
			this.textEditorData.VAdjustment.ValueChanged += VAdjustmentValueChanged;
		}

		protected override bool CanRaiseEvents {
			get {
				return true;
			}
		}

		internal TextArea (TextDocument doc, ITextEditorOptions options, EditMode initialMode)
		{
			//GtkWorkarounds.FixContainerLeak (this);
			base.CanGetFocus = true;
			
			// This is required to properly handle resizing and rendering of children
			//ResizeMode = ResizeMode.Queue;
		}

		TextEditor editor;
		internal void Initialize (TextEditor editor, TextDocument doc, ITextEditorOptions options, EditMode initialMode)
		{
			if (doc == null)
				throw new ArgumentNullException ("doc");
			this.editor = editor;
			textEditorData = new TextEditorData (doc);
			textEditorData.RecenterEditor += delegate {
				CenterToCaret ();
				StartCaretPulseAnimation ();
			};
			textEditorData.Document.TextReplaced += OnDocumentStateChanged;
			textEditorData.Document.TextSet += OnTextSet;
			textEditorData.Document.LineChanged += UpdateLinesOnTextMarkerHeightChange; 
			textEditorData.Document.MarkerAdded += HandleTextEditorDataDocumentMarkerChange;
			textEditorData.Document.MarkerRemoved += HandleTextEditorDataDocumentMarkerChange;
			
			textEditorData.CurrentMode = initialMode;
			
			this.textEditorData.Options = options ?? TextEditorOptions.DefaultOptions;


			textEditorData.Parent = editor;

			iconMargin = new IconMargin (editor);
			gutterMargin = new GutterMargin (editor);
//			dashedLineMargin = new DashedLineMargin (this);
			foldMarkerMargin = new FoldMarkerMargin (editor);
			textViewMargin = new TextViewMargin (editor);

			margins.Add (iconMargin);
			margins.Add (gutterMargin);
			margins.Add (foldMarkerMargin);
//			margins.Add (dashedLineMargin);
			
			margins.Add (textViewMargin);
			this.textEditorData.SelectionChanged += TextEditorDataSelectionChanged;
			this.textEditorData.UpdateAdjustmentsRequested += TextEditorDatahandleUpdateAdjustmentsRequested;
			Document.DocumentUpdated += DocumentUpdatedHandler;
			
			this.textEditorData.Options.Changed += OptionsChanged;
			
			/*TODO - Drag n drop
			Gtk.TargetList list = new Gtk.TargetList ();
			list.AddTextTargets (ClipboardActions.CopyOperation.TextType);
			Gtk.Drag.DestSet (this, DestDefaults.All, (TargetEntry[])list, DragAction.Move | DragAction.Copy);
			*/
			/*TODO
			imContext = new IMMulticontext ();
			imContext.Commit += IMCommit;
			
			imContext.UsePreedit = true;
			imContext.PreeditChanged += PreeditStringChanged;
			
			imContext.RetrieveSurrounding += delegate (object o, RetrieveSurroundingArgs args) {
				//use a single line of context, whole document would be very expensive
				//FIXME: UTF16 surrogates handling for caret offset? only matters for astral plane
				imContext.SetSurrounding (Document.GetLineText (Caret.Line, false), Caret.Column);
				args.RetVal = true;
			};
			
			imContext.SurroundingDeleted += delegate (object o, SurroundingDeletedArgs args) {
				//FIXME: UTF16 surrogates handling for offset and NChars? only matters for astral plane
				var line = Document.GetLine (Caret.Line);
				Document.Remove (line.Offset + args.Offset, args.NChars);
				args.RetVal = true;
			};
			*/

			/*TODO
			using (Pixmap inv = new Pixmap (null, 1, 1, 1)) {
				invisibleCursor = new Cursor (inv, inv, Gdk.Color.Zero, Gdk.Color.Zero, 0, 0);
			}*/
			
			InitAnimations ();
			this.Document.EndUndo += HandleDocumenthandleEndUndo;
			this.textEditorData.HeightTree.LineUpdateFrom += delegate(object sender, HeightTree.HeightChangedEventArgs e) {
				//Console.WriteLine ("redraw from :" + e.Line);
				RedrawFromLine (e.Line);
			};
			this.Document.Splitter.LineChanged += delegate(object sender, LineEventArgs e) {
				RedrawLine (e.Line.LineNumber);
			};

#if ATK
			TextEditorAccessible.Factory.Init (this);
#endif
			/*TODO
			if (GtkGestures.IsSupported) {
				this.AddGestureMagnifyHandler ((sender, args) => {
					Options.Zoom += Options.Zoom * (args.Magnification / 4d);
				});
			}*/
			OptionsChanged (this, EventArgs.Empty);
			Caret.PositionChanged += CaretPositionChanged;
		}

		public void RunAction (Action<TextEditorData> action)
		{
			try {
				action (GetTextEditorData ());
			} catch (Exception e) {
				Console.WriteLine ("Error while executing " + action + " :" + e);
			}
		}

		void HandleDocumenthandleEndUndo (object sender, TextDocument.UndoOperationEventArgs e)
		{
			if (this.Document.HeightChanged) {
				this.Document.HeightChanged = false;
				SetAdjustments ();
			}
		}

		void TextEditorDatahandleUpdateAdjustmentsRequested (object sender, EventArgs e)
		{
			SetAdjustments ();
		}
		
		/*
		public void ShowListWindow<T> (ListWindow<T> window, DocumentLocation loc)
		{
			var p = LocationToPoint (loc);
			var origin = window.Location;
	
			window.Location = new Point (origin.X + p.X - window.TextOffset , origin.Y + p.Y + (int)LineHeight);
			window.Show ();
		}*/
		
		internal int preeditOffset = -1, preeditLine, preeditCursorCharIndex;
		internal string preeditString;
		internal List<TextAttribute> preeditAttrs;
		internal bool preeditHeightChange;
		
		internal bool ContainsPreedit (int offset, int length)
		{
			if (string.IsNullOrEmpty (preeditString))
				return false;
			
			return offset <= preeditOffset && preeditOffset <= offset + length;
		}

		void PreeditStringChanged (object sender, EventArgs e)
		{
			//TODO imContext.GetPreeditString (out preeditString, out preeditAttrs, out preeditCursorCharIndex);
			if (!string.IsNullOrEmpty (preeditString)) {
				if (preeditOffset < 0) {
					preeditOffset = Caret.Offset;
					preeditLine = Caret.Line;
				}
				if (UpdatePreeditLineHeight ())
					QueueDraw ();
			} else {
				preeditOffset = -1;
				preeditString = null;
				preeditAttrs = null;
				preeditCursorCharIndex = 0;
				if (UpdatePreeditLineHeight ())
					QueueDraw ();
			}
			this.textViewMargin.ForceInvalidateLine (preeditLine);
			this.textEditorData.Document.CommitLineUpdate (preeditLine);
		}

		internal bool UpdatePreeditLineHeight ()
		{
			if (!string.IsNullOrEmpty (preeditString)) {
				using (var preeditLayout = new TextLayout(this)) {
					preeditLayout.Text = (preeditString);
					if (preeditAttrs != null)
						foreach (var attr in preeditAttrs)
							preeditLayout.AddAttribute (attr);

					var calcHeight = System.Math.Ceiling (preeditLayout.GetSize().Height/* / Pango.Scale.PangoScale*/);
					if (LineHeight < calcHeight) {
						textEditorData.HeightTree.SetLineHeight (preeditLine, calcHeight);
						preeditHeightChange = true;
						return true;
					}
				}
			} else if (preeditHeightChange) {
				preeditHeightChange = false;
				textEditorData.HeightTree.Rebuild ();
				return true;
			}
			return false;
		}

		void CaretPositionChanged (object sender, DocumentLocationEventArgs args) 
		{
			HideTooltip ();
			ResetIMContext ();
			
			if (Caret.AutoScrollToCaret && HasFocus)
				ScrollToCaret ();
			
//			Rectangle rectangle = textViewMargin.GetCaretRectangle (Caret.Mode);
			RequestResetCaretBlink ();
			
			textEditorData.CurrentMode.InternalCaretPositionChanged (textEditorData.Parent, textEditorData);
			
			if (!IsSomethingSelected) {
				if (/*Options.HighlightCaretLine && */args.Location.Line != Caret.Line) 
					RedrawMarginLine (TextViewMargin, args.Location.Line);
				RedrawMarginLine (TextViewMargin, Caret.Line);
			}
		}
		
		Selection oldSelection = Selection.Empty;
		void TextEditorDataSelectionChanged (object sender, EventArgs args)
		{
			if (IsSomethingSelected) {
				var selectionRange = MainSelection.GetSelectionRange (textEditorData);
				if (selectionRange.Offset >= 0 && selectionRange.EndOffset < Document.TextLength) {
					ClipboardActions.CopyToPrimary (this.textEditorData);
				} else {
					ClipboardActions.ClearPrimary ();
				}
			} else {
				ClipboardActions.ClearPrimary ();
			}
			// Handle redraw
			Selection selection = MainSelection;
			int startLine    = !selection.IsEmpty ? selection.Anchor.Line : -1;
			int endLine      = !selection.IsEmpty ? selection.Lead.Line : -1;
			int oldStartLine = !oldSelection.IsEmpty ? oldSelection.Anchor.Line : -1;
			int oldEndLine   = !oldSelection.IsEmpty ? oldSelection.Lead.Line : -1;
			if (SelectionMode == SelectionMode.Block) {
				this.RedrawMarginLines (this.textViewMargin, 
				                        System.Math.Min (System.Math.Min (oldStartLine, oldEndLine), System.Math.Min (startLine, endLine)),
				                        System.Math.Max (System.Math.Max (oldStartLine, oldEndLine), System.Math.Max (startLine, endLine)));
			} else {
				if (endLine < 0 && startLine >=0)
					endLine = Document.LineCount;
				if (oldEndLine < 0 && oldStartLine >=0)
					oldEndLine = Document.LineCount;
				int from = oldEndLine, to = endLine;
				if (!selection.IsEmpty && !oldSelection.IsEmpty) {
					if (startLine != oldStartLine && endLine != oldEndLine) {
						from = System.Math.Min (startLine, oldStartLine);
						to   = System.Math.Max (endLine, oldEndLine);
					} else if (startLine != oldStartLine) {
						from = startLine;
						to   = oldStartLine;
					} else if (endLine != oldEndLine) {
						from = endLine;
						to   = oldEndLine;
					} else if (startLine == oldStartLine && endLine == oldEndLine)  {
						if (selection.Anchor == oldSelection.Anchor) {
							this.RedrawMarginLine (this.textViewMargin, endLine);
						} else if (selection.Lead == oldSelection.Lead) {
							this.RedrawMarginLine (this.textViewMargin, startLine);
						} else { // 3rd case - may happen when changed programmatically
							this.RedrawMarginLine (this.textViewMargin, endLine);
							this.RedrawMarginLine (this.textViewMargin, startLine);
						}
						from = to = -1;
					}
				} else {
					if (selection.IsEmpty) {
						from = oldStartLine;
						to = oldEndLine;
					} else if (oldSelection.IsEmpty) {
						from = startLine;
						to = endLine;
					} 
				}
				
				if (from >= 0 && to >= 0) {
					this.RedrawMarginLines (this.textViewMargin, 
					                        System.Math.Max (0, System.Math.Min (from, to) - 1),
					                        System.Math.Max (from, to));
				}
			}
			oldSelection = selection;
			OnSelectionChanged (EventArgs.Empty);
		}
		
		internal void ResetIMContext ()
		{
			if (imContextNeedsReset) {
				//imContext.Reset ();
				imContextNeedsReset = false;
			}
		}
		/*TODO
		void IMCommit (object sender, Gtk.CommitArgs ca)
		{
			if (!IsRealized || !IsFocus)
				return;
			
			//this, if anywhere, is where we should handle UCS4 conversions
			for (int i = 0; i < ca.Str.Length; i++) {
				int utf32Char;
				if (char.IsHighSurrogate (ca.Str, i)) {
					utf32Char = char.ConvertToUtf32 (ca.Str, i);
					i++;
				} else {
					utf32Char = (int)ca.Str [i];
				}
				
				//include the other pre-IM state *if* the post-IM char matches the pre-IM (key-mapped) one
				 if (lastIMEventMappedChar == utf32Char && lastIMEventMappedChar == (uint)lastIMEventMappedKey) {
					editor.OnIMProcessedKeyPressEvent (lastIMEventMappedKey, lastIMEventMappedChar, lastIMEventMappedModifier);
				} else {
					editor.OnIMProcessedKeyPressEvent ((Gdk.Key)0, (uint)utf32Char, Gdk.ModifierType.None);
				}
			}
			
			//the IME can commit while there's still a pre-edit string
			//since we cached the pre-edit offset when it started, need to update it
			if (preeditOffset > -1) {
				preeditOffset = Caret.Offset;
			}
		}
		*/

		protected override void OnGotFocus (EventArgs args)
		{
			base.OnGotFocus (args);
			imContextNeedsReset = true;
			//IMContext.FocusIn ();
			RequestResetCaretBlink ();
			Document.CommitLineUpdate (Caret.Line);
		}
		
		System.Timers.Timer focusOutTimerId;
		void RemoveFocusOutTimerId ()
		{
			if (focusOutTimerId == null)
				return;

			focusOutTimerId.Stop ();
		}

		protected override void OnLostFocus (EventArgs args)
		{
			base.OnLostFocus (args);
			imContextNeedsReset = true;
			//imContext.FocusOut ();
			RemoveFocusOutTimerId ();

			if (tipWindow != null && currentTooltipProvider != null) {
				if (!currentTooltipProvider.IsInteractive (textEditorData.Parent, tipWindow))
					DelayedHideTooltip ();
			} else {
				HideTooltip ();
			}

			TextViewMargin.StopCaretThread ();
			Document.CommitLineUpdate (Caret.Line);
		}

		/*protected override void OnRealized ()
		{


			WidgetFlags |= WidgetFlags.Realized;
			WindowAttr attributes = new WindowAttr () {
				WindowType = Gdk.WindowType.Child,
				X = Allocation.X,
				Y = Allocation.Y,
				Width = Allocation.Width,
				Height = Allocation.Height,
				Wclass = WindowClass.InputOutput,
				Visual = this.Visual,
				Colormap = this.Colormap,
				EventMask = (int)(this.Events | Gdk.EventMask.ExposureMask),
				Mask = this.Events | Gdk.EventMask.ExposureMask,
			};
			
			WindowAttributesType mask = WindowAttributesType.X | WindowAttributesType.Y | WindowAttributesType.Colormap | WindowAttributesType.Visual;
			GdkWindow = new Gdk.Window (ParentWindow, attributes, mask);
			GdkWindow.UserData = Raw;
			Style = Style.Attach (GdkWindow);

			imContext.ClientWindow = this.GdkWindow;
			Caret.PositionChanged += CaretPositionChanged;
		}	*/
		/*
		protected override void OnUnrealized ()
		{
			imContext.ClientWindow = null;
			CancelScheduledHide ();
			base.OnUnrealized ();
		}*/
		
		void DocumentUpdatedHandler (object sender, EventArgs args)
		{
			foreach (DocumentUpdateRequest request in Document.UpdateRequests) {
				request.Update (textEditorData.Parent);
			}
		}
		
		public event EventHandler EditorOptionsChanged;

		protected virtual void OptionsChanged (object sender, EventArgs args)
		{
			if (Options == null)
				return;
			if (currentStyleName != Options.ColorScheme) {
				currentStyleName = Options.ColorScheme;
				this.textEditorData.ColorStyle = Options.GetColorStyle ();
				SetWidgetBgFromStyle ();
			}
			
			iconMargin.IsVisible   = Options.ShowIconMargin;
			gutterMargin.IsVisible     = Options.ShowLineNumberMargin;
			foldMarkerMargin.IsVisible = Options.ShowFoldMargin || Options.EnableQuickDiff;
//			dashedLineMargin.IsVisible = foldMarkerMargin.IsVisible || gutterMargin.IsVisible;
			
			if (EditorOptionsChanged != null)
				EditorOptionsChanged (this, args);
			
			textViewMargin.OptionsChanged ();
			foreach (Margin margin in this.margins) {
				if (margin == textViewMargin)
					continue;
				margin.OptionsChanged ();
			}
			SetAdjustments (Bounds);
			textEditorData.HeightTree.Rebuild ();
			//this.QueueResize ();
		}

		void SetWidgetBgFromStyle ()
		{
			// This is a hack around a problem with repainting the drag widget.
			// When this is not set a white square is drawn when the drag widget is moved
			// when the bg color is differs from the color style bg color (e.g. oblivion style)
			if (this.textEditorData.ColorStyle != null/* && GdkWindow != null*/) {
				settingWidgetBg = true; //prevent infinite recusion
				
				base.BackgroundColor= this.textEditorData.ColorStyle.PlainText.Background;
				settingWidgetBg = false;
			}
		}
		
		bool settingWidgetBg = false;
		/*protected override void OnStyleSet (Gtk.Style previous_style)
		{
			base.OnStyleSet (previous_style);
			if (!settingWidgetBg && textEditorData.ColorStyle != null) {
//				textEditorData.ColorStyle.UpdateFromGtkStyle (this.Style);
				SetWidgetBgFromStyle ();
			}
		}*/
		/*TODO - Make an onvisibility event handler to XWT windows or widgets
		protected override bool OnVisibilityNotifyEvent (EventVisibility evnt)
		{
			if (evnt.State == VisibilityState.FullyObscured)
				HideTooltip ();
			return base.OnVisibilityNotifyEvent (evnt);
		}*/
		protected override void Dispose (bool disposing)
		{
			if (popupWindow != null)
				popupWindow.Dispose ();

			HideTooltip ();
			Document.EndUndo -= HandleDocumenthandleEndUndo;
			Document.TextReplaced -= OnDocumentStateChanged;
			Document.TextSet -= OnTextSet;
			Document.LineChanged -= UpdateLinesOnTextMarkerHeightChange; 
			Document.MarkerAdded -= HandleTextEditorDataDocumentMarkerChange;
			Document.MarkerRemoved -= HandleTextEditorDataDocumentMarkerChange;

			DisposeAnimations ();
			
			RemoveFocusOutTimerId ();
			RemoveScrollWindowTimer ();
			/*if (invisibleCursor != null)
				invisibleCursor.Dispose ();
			*/
			Caret.PositionChanged -= CaretPositionChanged;
			
			Document.DocumentUpdated -= DocumentUpdatedHandler;
			if (textEditorData.Options != null)
				textEditorData.Options.Changed -= OptionsChanged;
			/*
			if (imContext != null){
				ResetIMContext ();
				imContext = imContext.Kill (x => x.Commit -= IMCommit);
			}
			*/
			UnregisterAdjustments ();

			foreach (Margin margin in this.margins) {
				if (margin is IDisposable)
					((IDisposable)margin).Dispose ();
			}
			textEditorData.ClearTooltipProviders ();
			
			this.textEditorData.SelectionChanged -= TextEditorDataSelectionChanged;
			this.textEditorData.Dispose (); 
			longestLine = null;

			base.Dispose (disposing);
		}
		
		[Obsolete("This method has been moved to TextEditorData. Will be removed in future versions.")]
		public void ClearTooltipProviders ()
		{
			textEditorData.ClearTooltipProviders ();
		}
		
		[Obsolete("This method has been moved to TextEditorData. Will be removed in future versions.")]
		public void AddTooltipProvider (TooltipProvider provider)
		{
			textEditorData.AddTooltipProvider (provider);
		}
		
		[Obsolete("This method has been moved to TextEditorData. Will be removed in future versions.")]
		public void RemoveTooltipProvider (TooltipProvider provider)
		{
			textEditorData.RemoveTooltipProvider (provider);
		}

		internal void RedrawMargin (Margin margin)
		{
			if (isDisposed)
				return;
			QueueDrawArea (margin.XOffset, 0, GetMarginWidth (margin),  this.Size.Height);
		}
		
		public void RedrawMarginLine (Margin margin, int logicalLine)
		{
			if (isDisposed)
				return;
			
			var y = LineToY (logicalLine) - this.textEditorData.VAdjustment.Value;
			var h = GetLineHeight (logicalLine);

			if (y + h > 0)
				QueueDrawArea (margin.XOffset, y, GetMarginWidth (margin), h);
		}

		double GetMarginWidth (Margin margin)
		{
			if (margin.Width < 0)
				return Size.Width - margin.XOffset;
			return margin.Width;
		}
		
		internal void RedrawLine (int logicalLine)
		{
			if (isDisposed || logicalLine > LineCount || logicalLine < DocumentLocation.MinLine)
				return;
			var y = LineToY (logicalLine) - this.textEditorData.VAdjustment.Value;
			var h = GetLineHeight (logicalLine);

			if (y + h > 0)
				QueueDrawArea (0, y, this.Size.Width, h);
		}
		
		public void QueueDrawArea (double x, double y, double w, double h)
		{
			base.QueueDraw(new Rectangle (x, y, w, h));
#if DEBUG_EXPOSE
				Console.WriteLine ("invalidated {0},{1} {2}x{3}", x, y, w, h);
#endif

		}
		
		public new void QueueDraw ()
		{
			base.QueueDraw ();
#if DEBUG_EXPOSE
				Console.WriteLine ("invalidated entire widget");
#endif
		}
		
		internal void RedrawPosition (int logicalLine, int logicalColumn)
		{
			if (isDisposed)
				return;
//				Console.WriteLine ("Redraw position: logicalLine={0}, logicalColumn={1}", logicalLine, logicalColumn);
			RedrawLine (logicalLine);
		}
		
		public void RedrawMarginLines (Margin margin, int start, int end)
		{
			if (isDisposed)
				return;
			if (start < 0)
				start = 0;
			double visualStart = -this.textEditorData.VAdjustment.Value + LineToY (start);
			if (end < 0)
				end = Document.LineCount;
			double visualEnd   = -this.textEditorData.VAdjustment.Value + LineToY (end) + GetLineHeight (end);
			QueueDrawArea ((int)margin.XOffset, (int)visualStart, GetMarginWidth (margin), (int)(visualEnd - visualStart));
		}
			
		internal void RedrawLines (int start, int end)
		{
//			Console.WriteLine ("redraw lines: start={0}, end={1}", start, end);
			if (isDisposed)
				return;
			if (start < 0)
				start = 0;
			var visualStart = -this.textEditorData.VAdjustment.Value +  LineToY (start);
			if (end < 0)
				end = Document.LineCount;
			var visualEnd   = -this.textEditorData.VAdjustment.Value + LineToY (end) + GetLineHeight (end);
			QueueDrawArea (0, visualStart, this.Size.Width, (visualEnd - visualStart));
		}
		
		public void RedrawFromLine (int logicalLine)
		{
//			Console.WriteLine ("Redraw from line: logicalLine={0}", logicalLine);
			if (isDisposed)
				return;
			var y = System.Math.Max (0, -this.textEditorData.VAdjustment.Value + LineToY (logicalLine));
			var sz = Size;
			QueueDrawArea (0, y, sz.Width, sz.Height - y);
		}


		/// <summary>Handles key input after key mapping and input methods.</summary>
		/// <param name="key">The mapped keycode.</param>
		/// <param name="unicodeChar">A UCS4 character. If this is nonzero, it overrides the keycode.</param>
		/// <param name="modifier">Keyboard modifier, excluding any consumed by key mapping or IM.</param>
		public void SimulateKeyPress (Key key, uint unicodeChar, ModifierKeys modifier)
		{/*
			ModifierType filteredModifiers = modifier & (ModifierType.ShiftMask | ModifierType.Mod1Mask
				 | ModifierType.ControlMask | ModifierType.MetaMask | ModifierType.SuperMask);
			*/CurrentMode.InternalHandleKeypress (textEditorData.Parent, textEditorData, key, unicodeChar, modifier);
			RequestResetCaretBlink ();
		}
		
		/*bool IMFilterKeyPress (Gdk.EventKey evt, Gdk.Key mappedKey, uint mappedChar, Gdk.ModifierType mappedModifiers)
		{
			if (lastIMEvent == evt)
				return false;
			
			if (evt.Type == EventType.KeyPress) {
				lastIMEvent = evt;
				lastIMEventMappedChar = mappedChar;
				lastIMEventMappedKey = mappedKey;
				lastIMEventMappedModifier = mappedModifiers;
			}
			
			if (imContext.FilterKeypress (evt)) {
				imContextNeedsReset = true;
				return true;
			} else {
				return false;
			}
		}*/
		
		CursorType invisibleCursor;
		
		internal void HideMouseCursor ()
		{
			base.Cursor = invisibleCursor;
		}

		ModifierKeys lastModifierKeys;

		protected override void OnKeyPressed (KeyEventArgs args)
		{
			var key = args.Key;
			var mod = lastModifierKeys = args.Modifiers;
			/*
			KeyboardShortcut[] accels;
			GtkWorkarounds.MapKeys (evt, out key, out mod, out accels);
			*/

			//HACK: we never call base.OnKeyPressEvent, so implement the popup key manually
			if (key == Key.Menu || (key == Key.F10 && mod.HasFlag (ModifierKeys.Shift))) {
				OnPopupMenu ();
				return;
			}

			uint keyVal = (uint)key;
			//CurrentMode.SelectValidShortcut (accels, out key, out mod);
			if (key == Key.F1 && (mod & (ModifierKeys.Control | ModifierKeys.Shift)) != 0) {
				var p = LocationToPoint (Caret.Location);
				ShowTooltip (ModifierKeys.None, Caret.Offset, p.X, p.Y);
				return;
			}
			if (key == Key.F2 && textViewMargin.IsCodeSegmentPreviewWindowShown) {
				textViewMargin.OpenCodeSegmentEditor ();
				return;
			}

			var unicodeChar = (char) (keyVal);

			//FIXME: why are we doing this?
			if ((key == Key.Space || unicodeChar == '(' || unicodeChar == ')') && (mod & ModifierKeys.Shift) != 0)
				mod = ModifierKeys.None;



			/*if (CurrentMode.WantsToPreemptIM || CurrentMode.PreemptIM (key, unicodeChar, mod)) {
				ResetIMContext ();
				//FIXME: should call base.OnKeyPressEvent when SimulateKeyPress didn't handle the event
				SimulateKeyPress (key, unicodeChar, mod);
				return true;
			}

			bool filter = IMFilterKeyPress (evt, key, unicodeChar, mod);
			if (filter)
				return true;*/

			//FIXME: OnIMProcessedKeyPressEvent should return false when it didn't handle the event
			if (editor.OnIMProcessedKeyPressEvent (key, unicodeChar, mod))
				return;

			base.OnKeyPressed (args);
		}

		protected override void OnKeyReleased (KeyEventArgs args)
		{
			lastModifierKeys = ModifierKeys.None;
			base.OnKeyReleased (args);
		}
		/*
		protected override bool OnKeyReleaseEvent (EventKey evnt)
		{
			if (IMFilterKeyPress (evnt, 0, 0, ModifierType.None)) {
				imContextNeedsReset = true;
			}
			return true;
		}*/
		
		PointerButton mouseButtonPressed;
		//uint lastTime;
		double pressPositionX, pressPositionY;
		protected override void OnButtonPressed (ButtonEventArgs e)
		{
			if (overChildWidget)
				return;

			pressPositionX = e.X;
			pressPositionY = e.Y;
			SetFocus ();


			if (e.MultiplePress < 1) {// filter double clicks
				/*if (e.Type == EventType.TwoButtonPress) {
					lastTime = e.Time;
				} else {
					lastTime = 0;
				}*/
				mouseButtonPressed = e.Button;
				double startPos;
				Margin margin = GetMarginAtX (e.X, out startPos);
				if (margin == textViewMargin) {
					//main context menu
					if (DoPopupMenu != null && e.Button == PointerButton.Right) {
						DoClickedPopupMenu (e);
						return;
					}
				}
				if (margin != null) 
					margin.MousePressed (new MarginMouseEventArgs (textEditorData.Parent, e, e.X - startPos, e.Y, lastModifierKeys));
			}


			base.OnButtonPressed (e);
		}
		
		bool DoClickedPopupMenu (ButtonEventArgs e)
		{
			double tmOffset = e.X - textViewMargin.XOffset;
			if (tmOffset >= 0) {
				DocumentLocation loc;
				if (textViewMargin.CalculateClickLocation (tmOffset, e.Y, out loc)) {
					if (!this.IsSomethingSelected || !this.SelectionRange.Contains (Document.LocationToOffset (loc))) {
						Caret.Location = loc;
					}
				}
				DoPopupMenu (e);
				this.ResetMouseState ();
				return true;
			}
			return false;
		}
		
		public Action<ButtonEventArgs> DoPopupMenu { get; set; }

		protected void OnPopupMenu ()
		{
			if (DoPopupMenu != null) {
				DoPopupMenu (null);
				return;
			}
		}

		public Margin LockedMargin {
			get;
			set;
		}

		Margin GetMarginAtX (double x, out double startingPos)
		{
			double curX = 0;
			foreach (Margin margin in this.margins) {
				if (!margin.IsVisible)
					continue;
				if (LockedMargin != null) {
					if (LockedMargin == margin) {
						startingPos = curX;
						return margin;
					}
				} else {
					if (curX <= x && (x <= curX + margin.Width || margin.Width < 0)) {
						startingPos = curX;
						return margin;
					}
				}
				curX += margin.Width;
			}
			startingPos = -1;
			return null;
		}

		protected override void OnButtonReleased (ButtonEventArgs e)
		{
			RemoveScrollWindowTimer ();

			//main context menu
			if (DoPopupMenu != null && e.Button == PointerButton.Right) {
				return;
			}

			double startPos;
			Margin margin = GetMarginAtX (e.X, out startPos);
			if (margin != null)
				margin.MouseReleased (new MarginMouseEventArgs (textEditorData.Parent, e, e.X - startPos, e.Y, lastModifierKeys));
			ResetMouseState ();

			base.OnButtonReleased (e);
		}
		
		protected void ResetMouseState ()
		{
			mouseButtonPressed = 0;
			textViewMargin.inDrag = false;
			textViewMargin.InSelectionDrag = false;
		}
		
		bool dragOver = false;
		ClipboardActions.CopyOperation dragContents = null;
		DocumentLocation defaultCaretPos, dragCaretPos;
		Selection selection = Selection.Empty;
		
		public bool IsInDrag {
			get {
				return dragOver;
			}
		}
		
		public void CaretToDragCaretPosition ()
		{
			Caret.Location = defaultCaretPos = dragCaretPos;
		}
		/*
		protected override void OnDragLeave (DragContext context, uint time_)
		{
			if (dragOver) {
				Caret.PreserveSelection = true;
				Caret.Location = defaultCaretPos;
				Caret.PreserveSelection = false;
				ResetMouseState ();
				dragOver = false;
			}
			base.OnDragLeave (context, time_);
		}
		
		protected override void OnDragDataGet (DragContext context, SelectionData selection_data, uint info, uint time_)
		{
			if (this.dragContents != null) {
				this.dragContents.SetData (selection_data, info);
				this.dragContents = null;
			}
			base.OnDragDataGet (context, selection_data, info, time_);
		}

		protected override void OnDragDataReceived (DragContext context, int x, int y, SelectionData selection_data, uint info, uint time_)
		{
			var undo = OpenUndoGroup ();
			int dragOffset = Document.LocationToOffset (dragCaretPos);
			if (context.Action == DragAction.Move) {
				if (CanEdit (Caret.Line) && !selection.IsEmpty) {
					var selectionRange = selection.GetSelectionRange (textEditorData);
					if (selectionRange.Offset < dragOffset)
						dragOffset -= selectionRange.Length;
					Caret.PreserveSelection = true;
					textEditorData.DeleteSelection (selection);
					Caret.PreserveSelection = false;

					selection = Selection.Empty;
				}
			}
			if (selection_data.Length > 0 && selection_data.Format == 8) {
				Caret.Offset = dragOffset;
				if (CanEdit (dragCaretPos.Line)) {
					int offset = Caret.Offset;
					if (!selection.IsEmpty && selection.GetSelectionRange (textEditorData).Offset >= offset) {
						var start = Document.OffsetToLocation (selection.GetSelectionRange (textEditorData).Offset + selection_data.Text.Length);
						var end = Document.OffsetToLocation (selection.GetSelectionRange (textEditorData).Offset + selection_data.Text.Length + selection.GetSelectionRange (textEditorData).Length);
						selection = new Selection (start, end);
					}
					textEditorData.PasteText (offset, selection_data.Text, null, ref undo);
					Caret.Offset = offset + selection_data.Text.Length;
					MainSelection = new Selection (Document.OffsetToLocation (offset), Document.OffsetToLocation (offset + selection_data.Text.Length));
				}
				dragOver = false;
				context = null;
			}
			mouseButtonPressed = 0;
			undo.Dispose ();
			base.OnDragDataReceived (context, x, y, selection_data, info, time_);
		}
		
		protected override bool OnDragMotion (DragContext context, int x, int y, uint time)
		{
			if (!this.HasFocus)
				this.GrabFocus ();
			if (!dragOver) {
				defaultCaretPos = Caret.Location;
			}
			
			DocumentLocation oldLocation = Caret.Location;
			dragOver = true;
			Caret.PreserveSelection = true;
			dragCaretPos = PointToLocation (x - textViewMargin.XOffset, y);
			int offset = Document.LocationToOffset (dragCaretPos);
			if (!selection.IsEmpty && offset >= this.selection.GetSelectionRange (textEditorData).Offset && offset < this.selection.GetSelectionRange (textEditorData).EndOffset) {
				Gdk.Drag.Status (context, DragAction.Default, time);
				Caret.Location = defaultCaretPos;
			} else {
				Gdk.Drag.Status (context, (context.Actions & DragAction.Move) == DragAction.Move ? DragAction.Move : DragAction.Copy, time);
				Caret.Location = dragCaretPos; 
			}
			this.RedrawLine (oldLocation.Line);
			if (oldLocation.Line != Caret.Line)
				this.RedrawLine (Caret.Line);
			Caret.PreserveSelection = false;
			return base.OnDragMotion (context, x, y, time);
		}
		*/
		Margin oldMargin = null;
		bool overChildWidget;

		public event EventHandler BeginHover;

		protected virtual void OnBeginHover (EventArgs e)
		{
			var handler = BeginHover;
			if (handler != null)
				handler (this, e);
		}

		protected override void OnMouseMoved (MouseMovedEventArgs e)
		{
			OnBeginHover (EventArgs.Empty);
			try {
				// The coordinates have to be properly adjusted to the origin since
				// the event may come from a child widget

				var o = ScreenBounds;
				var y = e.Y - o.Y;
				var x = e.X - o.X;
				overChildWidget = Children.Any (w => GetChildBounds(w).Contains(x,y));

				RemoveScrollWindowTimer ();
				//Gdk.ModifierType mod = e.State;
				double startPos;
				Margin margin = GetMarginAtX (x, out startPos);
				/*if (textViewMargin.inDrag && margin == this.textViewMargin && Gtk.Drag.CheckThreshold (this, (int)pressPositionX, (int)pressPositionY, (int)x, (int)y)) {
					dragContents = new ClipboardActions.CopyOperation ();
					dragContents.CopyData (textEditorData);
					DragContext context = Gtk.Drag.Begin (this, ClipboardActions.CopyOperation.TargetList, DragAction.Move | DragAction.Copy, 1, e);
					if (!Platform.IsMac) {
						CodeSegmentPreviewWindow window = new CodeSegmentPreviewWindow (textEditorData.Parent, true, textEditorData.SelectionRange, 300, 300);
						Gtk.Drag.SetIconWidget (context, window, 0, 0);
					}
					selection = MainSelection;
					textViewMargin.inDrag = false;
				} else */{
					FireMotionEvent (x, y, lastModifierKeys);
					if (mouseButtonPressed != 0) {
						UpdateScrollWindowTimer (x, y, lastModifierKeys);
					}
				}
			} catch (Exception ex) {
				throw ex;
				//GLib.ExceptionManager.RaiseUnhandledException (ex, false);
			}

			base.OnMouseMoved (e);
		}
		
		System.Timers.Timer scrollWindowTimer;
		double scrollWindowTimer_x;
		double scrollWindowTimer_y;
		ModifierKeys scrollWindowTimer_mod;
		
		void UpdateScrollWindowTimer (double x, double y, ModifierKeys mod)
		{
			scrollWindowTimer_x = x;
			scrollWindowTimer_y = y;
			scrollWindowTimer_mod = mod;
			if (scrollWindowTimer == null) {
				scrollWindowTimer = new System.Timers.Timer (50);
				scrollWindowTimer.Elapsed += (object s, ElapsedEventArgs ea) => {
					FireMotionEvent (scrollWindowTimer_x, scrollWindowTimer_y, scrollWindowTimer_mod);
				};
			}
			scrollWindowTimer.Start ();
		}
		
		void RemoveScrollWindowTimer ()
		{
			scrollWindowTimer.Stop ();
		}
		
		//Gdk.ModifierType lastState = ModifierType.None;

		void FireMotionEvent (double x, double y, ModifierKeys state)
		{
			lastModifierKeys = state;
			mx = x - textViewMargin.XOffset;
			my = y;

			ShowTooltip (state);

			double startPos;
			Margin margin;
			if (textViewMargin.InSelectionDrag) {
				margin = textViewMargin;
				startPos = textViewMargin.XOffset;
			} else {
				margin = GetMarginAtX (x, out startPos);
				if (margin != null) {
					if (!overChildWidget)
						Cursor = margin.MarginCursor;
					else {
						// Set the default cursor when the mouse is over an embedded widget
						Cursor = CursorType.Arrow;
					}
				}
			}

			if (oldMargin != margin && oldMargin != null)
				oldMargin.MouseLeft ();
			
			if (margin != null) 
				margin.MouseHover (new MarginMouseEventArgs (textEditorData.Parent, null,
					mouseButtonPressed, x - startPos, y, state));
			oldMargin = margin;
		}

		#region CustomDrag (for getting dnd data from toolbox items for example)
		/*string     customText;
		Widget customSource;
		public void BeginDrag (string text, Widget source, DragContext context)
		{
			customText = text;
			customSource = source;
			source.DragDataGet += CustomDragDataGet;
			source.DragEnd     += CustomDragEnd;
		}
		void CustomDragDataGet (object sender, Gtk.DragDataGetArgs args) 
		{
			args.SelectionData.Text = customText;
		}
		void CustomDragEnd (object sender, Gtk.DragEndArgs args) 
		{
			customSource.DragDataGet -= CustomDragDataGet;
			customSource.DragEnd -= CustomDragEnd;
			customSource = null;
			customText = null;
		}*/
		#endregion
		bool isMouseTrapped = false;

		protected override void OnMouseEntered (EventArgs args)
		{
			isMouseTrapped = true;
			base.OnMouseEntered (args);
		}

		protected override void OnMouseExited (EventArgs args)
		{
			isMouseTrapped = false;
			if (tipWindow != null && currentTooltipProvider != null) {
				if (!currentTooltipProvider.IsInteractive (textEditorData.Parent, tipWindow))
					DelayedHideTooltip ();
			} else {
				HideTooltip ();
			}
			textViewMargin.HideCodeSegmentPreviewWindow ();

			Cursor = null;
			if (oldMargin != null)
				oldMargin.MouseLeft ();

			base.OnMouseExited (args);
		}

		public double LineHeight {
			get {
				return this.textEditorData.LineHeight;
			}
			internal set {
				this.textEditorData.LineHeight = value;
			}
		}
		
		public TextViewMargin TextViewMargin {
			get {
				return textViewMargin;
			}
		}
		
		public Margin IconMargin {
			get { return iconMargin; }
		}
		
		public DocumentLocation LogicalToVisualLocation (DocumentLocation location)
		{
			return textEditorData.LogicalToVisualLocation (location);
		}

		public DocumentLocation LogicalToVisualLocation (int line, int column)
		{
			return textEditorData.LogicalToVisualLocation (line, column);
		}
		
		public void CenterToCaret ()
		{
			CenterTo (Caret.Location);
		}
		
		public void CenterTo (int offset)
		{
			CenterTo (Document.OffsetToLocation (offset));
		}
		
		public void CenterTo (int line, int column)
		{
			CenterTo (new DocumentLocation (line, column));
		}
		
		public void CenterTo (DocumentLocation p)
		{
			if (isDisposed || p.Line < 0 || p.Line > Document.LineCount)
				return;
			var sz = Size;
			SetAdjustments (Bounds);
			//			Adjustment adj;
			//adj.Upper
			if (this.textEditorData.VAdjustment.UpperValue < sz.Height) {
				this.textEditorData.VAdjustment.Value = 0;
				return;
			}
			
			//	int yMargin = 1 * this.LineHeight;
			double caretPosition = LineToY (p.Line);
			caretPosition -= this.textEditorData.VAdjustment.PageSize / 2;

			// Make sure the caret position is inside the bounds. This avoids an unnecessary bump of the scrollview.
			// The adjustment does this check, but does it after assigning the value, so the value may be out of bounds for a while.
			if (caretPosition + this.textEditorData.VAdjustment.PageSize > this.textEditorData.VAdjustment.UpperValue)
				caretPosition = this.textEditorData.VAdjustment.UpperValue - this.textEditorData.VAdjustment.PageSize;

			this.textEditorData.VAdjustment.Value = caretPosition;
			
			if (this.textEditorData.HAdjustment.UpperValue < sz.Width)  {
				this.textEditorData.HAdjustment.Value = 0;
			} else {
				double caretX = ColumnToX (Document.GetLine (p.Line), p.Column);
				double textWith = sz.Width - textViewMargin.XOffset;
				if (this.textEditorData.HAdjustment.Value > caretX) {
					this.textEditorData.HAdjustment.Value = caretX;
				} else if (this.textEditorData.HAdjustment.Value + textWith < caretX + TextViewMargin.CharWidth) {
					double adjustment = System.Math.Max (0, caretX - textWith + TextViewMargin.CharWidth);
					this.textEditorData.HAdjustment.Value = adjustment;
				}
			}
		}

		public void ScrollTo (int offset)
		{
			ScrollTo (Document.OffsetToLocation (offset));
		}
		
		public void ScrollTo (int line, int column)
		{
			ScrollTo (new DocumentLocation (line, column));
		}

//		class ScrollingActor
//		{
//			readonly TextEditor editor;
//			readonly double targetValue;
//			readonly double initValue;
//			
//			public ScrollingActor (Mono.TextEditor.TextEditor editor, double targetValue)
//			{
//				this.editor = editor;
//				this.targetValue = targetValue;
//				this.initValue = editor.VAdjustment.Value;
//			}
//
//			public bool Step (Actor<ScrollingActor> actor)
//			{
//				if (actor.Expired) {
//					editor.VAdjustment.Value = targetValue;
//					return false;
//				}
//				var newValue = initValue + (targetValue - initValue) / 100   * actor.Percent;
//				editor.VAdjustment.Value = newValue;
//				return true;
//			}
//		}

		internal void SmoothScrollTo (double value)
		{
			this.textEditorData.VAdjustment.Value = value;
/*			Stage<ScrollingActor> scroll = new Stage<ScrollingActor> (50);
			scroll.UpdateFrequency = 10;
			var scrollingActor = new ScrollingActor (this, value);
			scroll.Add (scrollingActor, 50);

			scroll.ActorStep += scrollingActor.Step;
			scroll.Play ();*/
		}

		public void ScrollTo (DocumentLocation p)
		{
			if (isDisposed || p.Line < 0 || p.Line > Document.LineCount || inCaretScroll)
				return;
			inCaretScroll = true;
			try {
				if (this.textEditorData.VAdjustment.Upper < Allocation.Height) {
					this.textEditorData.VAdjustment.Value = 0;
				} else {
					double caretPosition = LineToY (p.Line);
					if (this.textEditorData.VAdjustment.Value > caretPosition) {
						this.textEditorData.VAdjustment.Value = caretPosition;
					} else if (this.textEditorData.VAdjustment.Value + this.textEditorData.VAdjustment.PageSize - this.LineHeight < caretPosition) {
						this.textEditorData.VAdjustment.Value = caretPosition - this.textEditorData.VAdjustment.PageSize + this.LineHeight;
					}
				}
				
				if (this.textEditorData.HAdjustment.Upper < Allocation.Width)  {
					this.textEditorData.HAdjustment.Value = 0;
				} else {
					double caretX = ColumnToX (Document.GetLine (p.Line), p.Column);
					double textWith = Allocation.Width - textViewMargin.XOffset;
					if (this.textEditorData.HAdjustment.Value > caretX) {
						this.textEditorData.HAdjustment.Value = caretX;
					} else if (this.textEditorData.HAdjustment.Value + textWith < caretX + TextViewMargin.CharWidth) {
						double adjustment = System.Math.Max (0, caretX - textWith + TextViewMargin.CharWidth);
						this.textEditorData.HAdjustment.Value = adjustment;
					}
				}
			} finally {
				inCaretScroll = false;
			}
		}

		/// <summary>
		/// Scrolls the editor as required for making the specified area visible 
		/// </summary>
		public void ScrollTo (Rectangle rect)
		{
			inCaretScroll = true;
			try {
				var vad = this.textEditorData.VAdjustment;
				if (vad.UpperValue < Size.Height) {
					vad.Value = 0;
				} else {
					if (vad.Value >= rect.Top) {
						vad.Value = rect.Top;
					} else if (vad.Value + vad.PageSize - rect.Height < rect.Top) {
						vad.Value = rect.Top - vad.PageSize + rect.Height;
					}
				}

				var had = this.textEditorData.HAdjustment;
				if (had.UpperValue < Size.Width)  {
					had.Value = 0;
				} else {
					if (had.Value >= rect.Left) {
						had.Value = rect.Left;
					} else if (had.Value + had.PageSize - rect.Width < rect.Left) {
						had.Value = rect.Left - had.PageSize + rect.Width;
					}
				}
			} finally {
				inCaretScroll = false;
			}
		}
		
		bool inCaretScroll = false;
		public void ScrollToCaret ()
		{
			ScrollTo (Caret.Location);
		}

		public void TryToResetHorizontalScrollPosition ()
		{
			int caretX = (int)ColumnToX (Document.GetLine (Caret.Line), Caret.Column);
			int textWith = Size.Width - (int)textViewMargin.XOffset;
			if (caretX < textWith - TextViewMargin.CharWidth) 
				this.textEditorData.HAdjustment.Value = 0;
		}

		protected override void OnBoundsChanged ()
		{
			base.OnBoundsChanged ();

			SetAdjustments (Bounds);
			sizeHasBeenAllocated = true;
			if (Options.WrapLines)
				textViewMargin.PurgeLayoutCache ();
			SetChildrenPositions (Bounds);
		}

		/*
		protected override void OnSizeAllocated (Rectangle allocation)
		{
			base.OnSizeAllocated (allocation);
			SetAdjustments (Allocation);
			sizeHasBeenAllocated = true;
			if (Options.WrapLines)
				textViewMargin.PurgeLayoutCache ();
			SetChildrenPositions (allocation);
		}*/

		long lastScrollTime;
		protected override void OnMouseScrolled (MouseScrolledEventArgs e)
		{
			var modifier = !Platform.IsMac? ModifierKeys.Control
				//Mac window manager already uses control-scroll, so use command
				//Command might be either meta or mod1, depending on GTK version
				: (ModifierKeys.Command);

			var hasZoomModifier = (lastModifierKeys & modifier) != 0;
			if (hasZoomModifier && lastScrollTime != 0 && (e.Timestamp - lastScrollTime) < 100)
				hasZoomModifier = false;

			if (hasZoomModifier) {
				if (e.Direction == ScrollDirection.Up)
					Options.ZoomIn ();
				else if (e.Direction == ScrollDirection.Down)
					Options.ZoomOut ();

				this.QueueDraw ();
				if (isMouseTrapped)
					FireMotionEvent (mx + textViewMargin.XOffset, my, lastModifierKeys);
				return true;
			}
			lastScrollTime = e.Timestamp;

			base.OnMouseScrolled (e);
		}


		
		void SetHAdjustment ()
		{
			textEditorData.HeightTree.Rebuild ();
			
			if (textEditorData.HAdjustment == null || Options == null)
				return;
			textEditorData.HAdjustment.ValueChanged -= HAdjustmentValueChanged;
			if (Options.WrapLines) {
				this.textEditorData.HAdjustment.SetBounds (0, 0, 0, 0, 0);
			} else {
				if (longestLine != null && this.textEditorData.HAdjustment != null) {
					double maxX = longestLineWidth;
					if (maxX > Allocation.Width)
						maxX += 2 * this.textViewMargin.CharWidth;
					double width = Allocation.Width - this.TextViewMargin.XOffset;
					var realMaxX = System.Math.Max (maxX, this.textEditorData.HAdjustment.Value + width);

					foreach (var containerChild in editor.containerChildren.Concat (containerChildren)) {
						if (containerChild.Child == this)
							continue;
						realMaxX = System.Math.Max (realMaxX, containerChild.X + containerChild.Child.Allocation.Width);
					}

					this.textEditorData.HAdjustment.SetBounds (
						0,
						realMaxX,
						this.textViewMargin.CharWidth,
						width,
						width);
					if (realMaxX < width)
						this.textEditorData.HAdjustment.Value = 0;
				}
			}
			textEditorData.HAdjustment.ValueChanged += HAdjustmentValueChanged;
		}
		
		internal void SetAdjustments ()
		{
			SetAdjustments (Bounds);
		}
		
		public const int EditorLineThreshold = 0;

		internal void SetAdjustments (Rectangle allocation)
		{
			SetHAdjustment ();
			
			if (this.textEditorData.VAdjustment != null) {
				double maxY = textEditorData.HeightTree.TotalHeight;
				if (maxY > allocation.Height)
					maxY += EditorLineThreshold * this.LineHeight;

				foreach (var containerChild in editor.containerChildren.Concat (containerChildren)) {
					maxY = System.Math.Max (maxY, containerChild.Y + containerChild.Child.SizeRequest().Height);
				}

				if (VAdjustment.Value > maxY - allocation.Height) {
					VAdjustment.Value = System.Math.Max (0, maxY - allocation.Height);
					QueueDraw ();
				}
				this.textEditorData.VAdjustment.SetBounds (0, 
				                                           System.Math.Max (allocation.Height, maxY), 
				                                           LineHeight,
				                                           allocation.Height,
				                                           allocation.Height);
				if (maxY < allocation.Height)
					this.textEditorData.VAdjustment.Value = 0;
			}
		}
		
		public int GetWidth (string text)
		{
			return this.textViewMargin.GetWidth (text);
		}
		
		void UpdateMarginXOffsets ()
		{
			double curX = 0;
			foreach (Margin margin in this.margins) {
				if (!margin.IsVisible)
					continue;
				margin.XOffset = curX;
				curX += margin.Width;
			}
		}
		
		void RenderMargins (Context cr/*, Cairo.Context textViewCr*/, Rectangle cairoRectangle)
		{
			this.TextViewMargin.rulerX = Options.RulerColumn * this.TextViewMargin.CharWidth - this.textEditorData.HAdjustment.Value;
			int startLine = YToLine (cairoRectangle.Y + this.textEditorData.VAdjustment.Value);
			double startY = LineToY (startLine);
			double curY = startY - this.textEditorData.VAdjustment.Value;
			bool setLongestLine = false;
			foreach (var margin in this.margins) {
				if (margin.BackgroundRenderer != null)
					margin.BackgroundRenderer.Draw (cr, cairoRectangle);
			}


			for (int visualLineNumber = textEditorData.LogicalToVisualLine (startLine);; visualLineNumber++) {
				int logicalLineNumber = textEditorData.VisualToLogicalLine (visualLineNumber);
				var line = Document.GetLine (logicalLineNumber);

				// Ensure that the correct line height is set.
				if (line != null)
					textViewMargin.GetLayout (line);

				double lineHeight = GetLineHeight (line);
				foreach (var margin in this.margins) {
					if (!margin.IsVisible)
						continue;
					try {
						margin.Draw (margin == textViewMargin ? textViewCr : cr, cairoRectangle, line, logicalLineNumber, margin.XOffset, curY, lineHeight);
					} catch (Exception e) {
						System.Console.WriteLine (e);
					}
				}
				// take the line real render width from the text view margin rendering (a line can consist of more than 
				// one line and be longer (foldings!) ex. : someLine1[...]someLine2[...]someLine3)
				double lineWidth = textViewMargin.lastLineRenderWidth + HAdjustment.Value;
				if (longestLine == null || lineWidth > longestLineWidth) {
					longestLine = line;
					longestLineWidth = lineWidth;
					setLongestLine = true;
				}
				curY += lineHeight;
				if (curY > cairoRectangle.Y + cairoRectangle.Height)
					break;
			}
			
			foreach (var margin in this.margins) {
				if (!margin.IsVisible)
					continue;
				foreach (var drawer in margin.MarginDrawer)
					drawer.Draw (cr, cairoRectangle);
			}
			
			if (setLongestLine) 
				SetHAdjustment ();
		}
		
		/*
		protected override bool OnWidgetEvent (Event evnt)
		{
			System.Console.WriteLine(evnt);
			return base.OnWidgetEvent (evnt);
		}*/
		
		double oldVadjustment = 0;
		
		void UpdateAdjustments ()
		{
			int lastVisibleLine = textEditorData.LogicalToVisualLine (Document.LineCount);
			if (oldRequest != lastVisibleLine) {
				SetAdjustments (this.Allocation);
				oldRequest = lastVisibleLine;
			}
		}

		protected override void OnDraw (Context cr, Rectangle dirtyRect)
		{
			if (this.isDisposed)
				return false;
			UpdateAdjustments ();

			/*FIXME: How to toggle AA in XWT?
			 * if (!Options.UseAntiAliasing) {
				textViewCr.Antialias = Cairo.Antialias.None;
				cr.Antialias = Cairo.Antialias.None;
			}*/

			UpdateMarginXOffsets ();

			cr.SetLineWidth(Options.Zoom);

			// textViewCr
			cr.Rectangle (textViewMargin.XOffset, 0, WidthRequest - textViewMargin.XOffset, HeightRequest);
			// textViewCr
			cr.Clip ();

			RenderMargins (cr, dirtyRect);

			if (requestResetCaretBlink && HasFocus) {
				textViewMargin.ResetCaretBlink (200);
				requestResetCaretBlink = false;
			}

			foreach (Animation animation in actors) {
				animation.Drawer.Draw (cr);
			}

			if (HasFocus)
				textViewMargin.DrawCaret (cr, dirtyRect);

			if (Painted != null)
				Painted (this, new PaintEventArgs (cr, dirtyRect));

			if (Caret.IsVisible)
				textViewMargin.DrawCaret (cr, dirtyRect);

			base.OnDraw (cr, dirtyRect);
		}

		public event EventHandler<PaintEventArgs> Painted;

		#region TextEditorData delegation
		public string EolMarker {
			get {
				return textEditorData.EolMarker;
			}
		}
		
		public Mono.TextEditor.Highlighting.ColorScheme ColorStyle {
			get {
				return this.textEditorData.ColorStyle;
			}
		}
		
		public EditMode CurrentMode {
			get {
				return this.textEditorData.CurrentMode;
			}
			set {
				this.textEditorData.CurrentMode = value;
			}
		}
		
		public bool IsSomethingSelected {
			get {
				return this.textEditorData.IsSomethingSelected;
			}
		}
		
		public Selection MainSelection {
			get {
				return textEditorData.MainSelection;
			}
			set {
				textEditorData.MainSelection = value;
			}
		}
		
		public SelectionMode SelectionMode {
			get {
				return textEditorData.SelectionMode;
			}
			set {
				textEditorData.SelectionMode = value;
			}
		}

		public TextSegment SelectionRange {
			get {
				return this.textEditorData.SelectionRange;
			}
			set {
				this.textEditorData.SelectionRange = value;
			}
		}
				
		public string SelectedText {
			get {
				return this.textEditorData.SelectedText;
			}
			set {
				this.textEditorData.SelectedText = value;
			}
		}
		
		public int SelectionAnchor {
			get {
				return this.textEditorData.SelectionAnchor;
			}
			set {
				this.textEditorData.SelectionAnchor = value;
			}
		}
		
		public IEnumerable<DocumentLine> SelectedLines {
			get {
				return this.textEditorData.SelectedLines;
			}
		}
		
		public ScrollAdjustment HAdjustment {
			get {
				return this.textEditorData.HAdjustment;
			}
		}
		
		public ScrollAdjustment VAdjustment {
			get {
				return this.textEditorData.VAdjustment;
			}
		}
		
		public int Insert (int offset, string value)
		{
			return textEditorData.Insert (offset, value);
		}
		
		public void Remove (DocumentRegion region)
		{
			textEditorData.Remove (region);
		}
		
		public void Remove (TextSegment removeSegment)
		{
			textEditorData.Remove (removeSegment);
		}

		public void Remove (int offset, int count)
		{
			textEditorData.Remove (offset, count);
		}
		
		public int Replace (int offset, int count, string value)
		{
			return textEditorData.Replace (offset, count, value);
		}
		
		public void ClearSelection ()
		{
			this.textEditorData.ClearSelection ();
		}
		
		public void DeleteSelectedText ()
		{
			this.textEditorData.DeleteSelectedText ();
		}
		
		public void DeleteSelectedText (bool clearSelection)
		{
			this.textEditorData.DeleteSelectedText (clearSelection);
		}
		
		public void RunEditAction (Action<TextEditorData> action)
		{
			action (this.textEditorData);
		}
		
		public void SetSelection (int anchorOffset, int leadOffset)
		{
			this.textEditorData.SetSelection (anchorOffset, leadOffset);
		}
		
		public void SetSelection (DocumentLocation anchor, DocumentLocation lead)
		{
			this.textEditorData.SetSelection (anchor, lead);
		}
			
		public void SetSelection (int anchorLine, int anchorColumn, int leadLine, int leadColumn)
		{
			this.textEditorData.SetSelection (anchorLine, anchorColumn, leadLine, leadColumn);
		}
		
		public void ExtendSelectionTo (DocumentLocation location)
		{
			this.textEditorData.ExtendSelectionTo (location);
		}
		public void ExtendSelectionTo (int offset)
		{
			this.textEditorData.ExtendSelectionTo (offset);
		}
		public void SetSelectLines (int from, int to)
		{
			this.textEditorData.SetSelectLines (from, to);
		}
		
		public void InsertAtCaret (string text)
		{
			textEditorData.InsertAtCaret (text);
		}
		
		public bool CanEdit (int line)
		{
			return textEditorData.CanEdit (line);
		}
		
		public string GetLineText (int line)
		{
			return textEditorData.GetLineText (line);
		}
		
		public string GetLineText (int line, bool includeDelimiter)
		{
			return textEditorData.GetLineText (line, includeDelimiter);
		}
		
		/// <summary>
		/// Use with care.
		/// </summary>
		/// <returns>
		/// A <see cref="TextEditorData"/>
		/// </returns>
		public TextEditorData GetTextEditorData ()
		{
			return this.textEditorData;
		}
		
		public event EventHandler SelectionChanged;
		protected virtual void OnSelectionChanged (EventArgs args)
		{
			CurrentMode.InternalSelectionChanged (editor, textEditorData);
			if (SelectionChanged != null) 
				SelectionChanged (this, args);
		}
		#endregion
		
		#region Document delegation
		public int Length {
			get {
				return Document.TextLength;
			}
		}

		public string Text {
			get {
				return Document.Text;
			}
			set {
				Document.Text = value;
			}
		}

		public string GetTextBetween (int startOffset, int endOffset)
		{
			return Document.GetTextBetween (startOffset, endOffset);
		}
		
		public string GetTextBetween (DocumentLocation start, DocumentLocation end)
		{
			return Document.GetTextBetween (start, end);
		}
		
		public string GetTextBetween (int startLine, int startColumn, int endLine, int endColumn)
		{
			return Document.GetTextBetween (startLine, startColumn, endLine, endColumn);
		}

		public string GetTextAt (int offset, int count)
		{
			return Document.GetTextAt (offset, count);
		}


		public string GetTextAt (TextSegment segment)
		{
			return Document.GetTextAt (segment);
		}
		
		public string GetTextAt (DocumentRegion region)
		{
			return Document.GetTextAt (region);
		}

		public char GetCharAt (int offset)
		{
			return Document.GetCharAt (offset);
		}
		
		public IEnumerable<DocumentLine> Lines {
			get {
				return Document.Lines;
			}
		}
		
		public int LineCount {
			get {
				return Document.LineCount;
			}
		}
		
		public int LocationToOffset (int line, int column)
		{
			return Document.LocationToOffset (line, column);
		}
		
		public int LocationToOffset (DocumentLocation location)
		{
			return Document.LocationToOffset (location);
		}
		
		public DocumentLocation OffsetToLocation (int offset)
		{
			return Document.OffsetToLocation (offset);
		}

		public string GetLineIndent (int lineNumber)
		{
			return Document.GetLineIndent (lineNumber);
		}
		
		public string GetLineIndent (DocumentLine segment)
		{
			return Document.GetLineIndent (segment);
		}
		
		public DocumentLine GetLine (int lineNumber)
		{
			return Document.GetLine (lineNumber);
		}
		
		public DocumentLine GetLineByOffset (int offset)
		{
			return Document.GetLineByOffset (offset);
		}
		
		public int OffsetToLineNumber (int offset)
		{
			return Document.OffsetToLineNumber (offset);
		}
		
		public IDisposable OpenUndoGroup()
		{
			return Document.OpenUndoGroup ();
		}
		#endregion
		
		#region Search & Replace
		
		bool highlightSearchPattern = false;
		
		public string SearchPattern {
			get {
				return this.textEditorData.SearchRequest.SearchPattern;
			}
			set {
				if (this.textEditorData.SearchRequest.SearchPattern != value) {
					this.textEditorData.SearchRequest.SearchPattern = value;
				}
			}
		}
		
		public ISearchEngine SearchEngine {
			get {
				return this.textEditorData.SearchEngine;
			}
			set {
				Debug.Assert (value != null);
				this.textEditorData.SearchEngine = value;
			}
		}
		
		public event EventHandler HighlightSearchPatternChanged;
		public bool HighlightSearchPattern {
			get {
				return highlightSearchPattern;
			}
			set {
				if (highlightSearchPattern != value) {
					this.highlightSearchPattern = value;
					if (HighlightSearchPatternChanged != null)
						HighlightSearchPatternChanged (this, EventArgs.Empty);
					textViewMargin.DisposeLayoutDict ();
					this.QueueDraw ();
				}
			}
		}
		
		public bool IsCaseSensitive {
			get {
				return this.textEditorData.SearchRequest.CaseSensitive;
			}
			set {
				this.textEditorData.SearchRequest.CaseSensitive = value;
			}
		}
		
		public bool IsWholeWordOnly {
			get {
				return this.textEditorData.SearchRequest.WholeWordOnly;
			}
			
			set {
				this.textEditorData.SearchRequest.WholeWordOnly = value;
			}
		}
		
		public TextSegment SearchRegion {
			get {
				return this.textEditorData.SearchRequest.SearchRegion;
			}
			
			set {
				this.textEditorData.SearchRequest.SearchRegion = value;
			}
		}
		
		public SearchResult SearchForward (int fromOffset)
		{
			return textEditorData.SearchForward (fromOffset);
		}
		
		public SearchResult SearchBackward (int fromOffset)
		{
			return textEditorData.SearchBackward (fromOffset);
		}
		
		class CaretPulseAnimation : IAnimationDrawer
		{
			TextEditor editor;
			
			public double Percent { get; set; }
			
			public Rectangle AnimationBounds {
				get {
					double x = editor.TextViewMargin.caretX;
					double y = editor.TextViewMargin.caretY;
					double extend = 100 * 5;
					int width = (int)(editor.TextViewMargin.charWidth + 2 * extend * editor.Options.Zoom / 2);
					return new Rectangle ((int)(x - extend * editor.Options.Zoom / 2), 
					                          (int)(y - extend * editor.Options.Zoom),
					                          width,
					                          (int)(editor.LineHeight + 2 * extend * editor.Options.Zoom));
				}
			}
			
			public CaretPulseAnimation (TextEditor editor)
			{
				this.editor = editor;
			}
			
			public void Draw (Context cr)
			{
				cr.Save ();
				double x = editor.TextViewMargin.caretX;
				double y = editor.TextViewMargin.caretY;
				if (editor.Caret.Mode != CaretMode.Block)
					x -= editor.TextViewMargin.charWidth / 2;
				var sz = editor.Size;
				cr.Rectangle (editor.TextViewMargin.XOffset, 0, sz.Width - editor.TextViewMargin.XOffset, sz.Height);
				cr.Clip ();

				double extend = Percent * 5;
				double width = editor.TextViewMargin.charWidth + 2 * extend * editor.Options.Zoom / 2;
				FoldingScreenbackgroundRenderer.DrawRoundRectangle (cr, true, true, 
				                                                    x - extend * editor.Options.Zoom / 2, 
				                                                    y - extend * editor.Options.Zoom, 
				                                                    System.Math.Min (editor.TextViewMargin.charWidth / 2, width), 
				                                                    width,
				                                                    editor.LineHeight + 2 * extend * editor.Options.Zoom);
				var color = editor.ColorStyle.PlainText.Foreground;
				color.Alpha = 0.8;
				cr.SetLineWidth(editor.Options.Zoom);
				cr.SetColor(color);
				cr.Stroke ();
				cr.Restore ();
			}
		}
		
		public enum PulseKind {
			In, Out, Bounce
		}
		
		public class RegionPulseAnimation : IAnimationDrawer
		{
			TextEditor editor;
			
			public PulseKind Kind { get; set; }
			public double Percent { get; set; }
			
			Rectangle region;
			
			public Rectangle AnimationBounds {
				get {
					int x = region.X;
					int y = region.Y;
					int animationPosition = (int)(100 * 100);
					int width = (int)(region.Width + 2 * animationPosition * editor.Options.Zoom / 2);
					
					return new Rectangle ((int)(x - animationPosition * editor.Options.Zoom / 2), 
					                          (int)(y - animationPosition * editor.Options.Zoom),
					                          width,
					                          (int)(region.Height + 2 * animationPosition * editor.Options.Zoom));
				}
			}
			
			public RegionPulseAnimation (TextEditor editor, Point position, Size size)
				: this (editor, new Rectangle (position, size)) {}
			
			public RegionPulseAnimation (TextEditor editor, Rectangle region)
			{
				if (region.X < 0 || region.Y < 0 || region.Width < 0 || region.Height < 0)
					throw new ArgumentException ("region is invalid");
				
				this.editor = editor;
				this.region = region;
			}
			
			public void Draw (Context cr)
			{
				cr.Save ();
				var x = region.X;
				var y = region.Y;
				int animationPosition = (int)(Percent * 100);
				var sz = editor.Size;
				cr.Rectangle (editor.TextViewMargin.XOffset, 0, sz.Width - editor.TextViewMargin.XOffset, sz.Height);
				cr.Clip ();

				int width = (int)(region.Width + 2 * animationPosition * editor.Options.Zoom / 2);
				FoldingScreenbackgroundRenderer.DrawRoundRectangle (cr, true, true, 
				                                                    (int)(x - animationPosition * editor.Options.Zoom / 2), 
				                                                    (int)(y - animationPosition * editor.Options.Zoom), 
				                                                    System.Math.Min (editor.TextViewMargin.charWidth / 2, width), 
				                                                    width,
				                                                    (int)(region.Height + 2 * animationPosition * editor.Options.Zoom));
				var color = editor.ColorStyle.PlainText.Foreground;
				color.Alpha = 0.8;
				cr.SetLineWidth(editor.Options.Zoom);
				cr.SetColor(color);
				cr.Stroke ();

				cr.Restore ();
			}
		}
		
		Rectangle RangeToRectangle (DocumentLocation start, DocumentLocation end)
		{
			if (start.Column < 0 || start.Line < 0 || end.Column < 0 || end.Line < 0)
				return Rectangle.Zero;

			var startPt = this.LocationToPoint (start);
			var endPt = this.LocationToPoint (end);
			var width = endPt.X - startPt.X;
			
			if (startPt.Y != endPt.Y || startPt.X < 0 || startPt.Y < 0 || width < 0)
				return Rectangle.Zero;
			
			return new Rectangle (startPt.X, startPt.Y, width, (int)this.LineHeight);
		}
		
		/// <summary>
		/// Initiate a pulse at the specified document location
		/// </summary>
		/// <param name="pulseLocation">
		/// A <see cref="DocumentLocation"/>
		/// </param>
		public void PulseCharacter (DocumentLocation pulseStart)
		{
			if (pulseStart.Column < 0 || pulseStart.Line < 0)
				return;
			var rect = RangeToRectangle (pulseStart, new DocumentLocation (pulseStart.Line, pulseStart.Column + 1));
			if (rect.X < 0 || rect.Y < 0 || System.Math.Max (rect.Width, rect.Height) <= 0)
				return;
			StartAnimation (new RegionPulseAnimation (editor, rect) {
				Kind = PulseKind.Bounce
			});
		}

		
		public SearchResult FindNext (bool setSelection)
		{
			SearchResult result = textEditorData.FindNext (setSelection);
			TryToResetHorizontalScrollPosition ();
			AnimateSearchResult (result);
			return result;
		}

		public void StartCaretPulseAnimation ()
		{
			StartAnimation (new CaretPulseAnimation (editor));
		}

		SearchHighlightPopupWindow popupWindow = null;
		
		public void StopSearchResultAnimation ()
		{
			if (popupWindow == null)
				return;
			popupWindow.StopPlaying ();
		}
		
		public void AnimateSearchResult (SearchResult result)
		{
			if (!Visible || !Options.EnableAnimations || result == null)
				return;
			
			// Don't animate multi line search results
			if (OffsetToLineNumber (result.Segment.Offset) != OffsetToLineNumber (result.Segment.EndOffset))
				return;
			
			TextViewMargin.MainSearchResult = result.Segment;
			if (!TextViewMargin.MainSearchResult.IsInvalid) {
				if (popupWindow != null) {
					popupWindow.StopPlaying ();
					popupWindow.Dispose ();
				}
				popupWindow = new SearchHighlightPopupWindow (editor);
				popupWindow.Result = result;
				popupWindow.Popup ();
				popupWindow.Disposed += delegate {
					popupWindow = null;
				};
			}
		}
		
		class SearchHighlightPopupWindow : BounceFadePopupWidget
		{
			public SearchResult Result {
				get;
				set;
			}
			
			public SearchHighlightPopupWindow (TextEditor editor) : base (editor)
			{
			}
			
			public /*override*/ void Popup ()
			{
				ExpandWidth = (uint)Editor.LineHeight;
				ExpandHeight = (uint)Editor.LineHeight / 2;
				BounceEasing = Easing.Sine;
				Duration = 150;
				base.Popup ();
			}
			
			protected /*override*/ void OnAnimationCompleted ()
			{
				base.OnAnimationCompleted ();
				Dispose ();
			}

			/*
			protected override void OnDestroyed ()
			{
				base.OnDestroyed ();
				if (layout != null)
					layout.Destroy ();
			}*/
			
			protected /*override*/ Rectangle CalculateInitialBounds ()
			{
				DocumentLine line = Editor.Document.GetLineByOffset (Result.Offset);
				int lineNr = Editor.Document.OffsetToLineNumber (Result.Offset);
				ISyntaxMode mode = Editor.Document.SyntaxMode != null && Editor.Options.EnableSyntaxHighlighting ? Editor.Document.SyntaxMode : new SyntaxMode (Editor.Document);
				int logicalRulerColumn = line.GetLogicalColumn (Editor.GetTextEditorData (), Editor.Options.RulerColumn);
				var lineLayout = Editor.TextViewMargin.CreateLinePartLayout (mode, line, logicalRulerColumn, line.Offset, line.Length, -1, -1);
				if (lineLayout == null)
					return new Rectangle ();
				
				int x1, x2;
				int index = Result.Offset - line.Offset - 1;
				if (index >= 0) {
					x1 = lineLayout.Layout.GetCoordinateFromIndex (index).X;
				} else {
					l = x1 = 0;
				}
				
				index = Result.Offset - line.Offset - 1 + Result.Length;
				if (index >= 0) {
					x2 = lineLayout.Layout.GetCoordinateFromIndex (index).X;
				} else {
					x2 = 0;
					Console.WriteLine ("Invalid end index :" + index);
				}
				
				double y = Editor.LineToY (lineNr);
				double w = (x2 - x1)/* / Pango.Scale.PangoScale*/;
				double x = (x1 /* / Pango.Scale.PangoScale*/ + Editor.TextViewMargin.XOffset + Editor.TextViewMargin.TextStartPosition);
				var h = Editor.LineHeight;

				//adjust the width to match TextViewMargin
				w = System.Math.Ceiling (w + 1);

				//add space for the shadow
				w += shadowOffset;
				h += shadowOffset;

				return new Rectangle (x, y, w, h);
			}

			const int shadowOffset = 1;

			TextLayout layout = null;

			protected /*override*/ void Draw (Context cr, Rectangle area)
			{/*TODO
				if (!Editor.Options.UseAntiAliasing)
					cr.Antialias = Cairo.Antialias.None;*/
				cr.SetLineWidth(Editor.Options.Zoom);

				if (layout == null) {
					layout = new TextLayout ();
					layout.Font = Editor.Options.Font;
					layout.Markup = Editor.GetTextEditorData ().GetMarkup (Result.Offset, Result.Length, true);
				}

				// subtract off the shadow again
				var width = area.Width - shadowOffset;
				var height = area.Height - shadowOffset;

				//from TextViewMargin's actual highlighting
				double corner = System.Math.Min (4, width) * Editor.Options.Zoom;

				//fill in the highlight rect with solid white to prevent alpha blending artifacts on the corners
				FoldingScreenbackgroundRenderer.DrawRoundRectangle (cr, true, true, 0, 0, corner, width, height);
				cr.SetColor(Colors.White);
				cr.Fill ();

				//draw the shadow
				FoldingScreenbackgroundRenderer.DrawRoundRectangle (cr, true, true,
					shadowOffset, shadowOffset, corner, width, height);
				var color = TextViewMargin.DimColor (Editor.ColorStyle.SearchResultMain.Color, 0.3);
				color.Alpha = 0.5 * opacity * opacity;
				cr.SetColor(color);
				cr.Fill ();

				//draw the highlight rectangle
				FoldingScreenbackgroundRenderer.DrawRoundRectangle (cr, true, true, 0, 0, corner, width, height);
				using (var gradient = new LinearGradient (0, 0, 0, height)) {
					color = ColorLerp (
						TextViewMargin.DimColor (Editor.ColorStyle.SearchResultMain.Color, 1.1),
						Editor.ColorStyle.SearchResultMain.Color,
						1 - opacity);
					gradient.AddColorStop (0, color);
					color = ColorLerp (
						TextViewMargin.DimColor (Editor.ColorStyle.SearchResultMain.Color, 0.9),
						Editor.ColorStyle.SearchResultMain.Color,
						1 - opacity);
					gradient.AddColorStop (1, color);
					cr.Pattern = gradient;
					cr.Fill ();
				}

				//and finally the text
				cr.SetColor(Colors.Black);
				cr.DrawTextLayout (layout, area.X, area.Y);
			}

			static Color ColorLerp (Color from, Color to, double scale)
			{
				return new Color (
					Lerp (from.Red, to.Red, scale),
					Lerp (from.Green, to.Green, scale),
					Lerp (from.Blue, to.Blue, scale),
					Lerp (from.Alpha, to.Alpha, scale)
				);
			}

			static double Lerp (double from, double to, double scale)
			{
				return from + scale * (to - from);
			}
		}
		
		public SearchResult FindPrevious (bool setSelection)
		{
			SearchResult result = textEditorData.FindPrevious (setSelection);
			TryToResetHorizontalScrollPosition ();
			AnimateSearchResult (result);
			return result;
		}
		
		public bool Replace (string withPattern)
		{
			return textEditorData.SearchReplace (withPattern, true);
		}
		
		public int ReplaceAll (string withPattern)
		{
			return textEditorData.SearchReplaceAll (withPattern);
		}
		#endregion
	
		#region Tooltips
		[Obsolete("This property has been moved to TextEditorData.  Will be removed in future versions.")]
		public IEnumerable<TooltipProvider> TooltipProviders {
			get { return textEditorData.TooltipProviders; }
		}

		// Tooltip fields
		const int TooltipTimeout = 650;
		TooltipItem tipItem;
		
		int tipX, tipY;
		uint tipHideTimeoutId = 0;
		uint tipShowTimeoutId = 0;
		static Window tipWindow;
		static TooltipProvider currentTooltipProvider;

		// Data for the next tooltip to be shown
		int nextTipOffset = 0;
		int nextTipX=0; int nextTipY=0;
		ModifierKeys nextTipModifierState = ModifierKeys.None;
		DateTime nextTipScheduledTime; // Time at which we want the tooltip to show
		
		void ShowTooltip (ModifierKeys modifierState)
		{
			if (mx < TextViewMargin.XOffset + TextViewMargin.TextStartPosition) {
				HideTooltip ();
				return;
			}

			var loc = PointToLocation (mx, my, true);
			if (loc.IsEmpty) {
				HideTooltip ();
				return;
			}

			// Hide editor tooltips for text marker extended regions (message bubbles)
			double y = LineToY (loc.Line);
			if (y + LineHeight < my) {
				HideTooltip ();
				return;
			}
			
			ShowTooltip (modifierState, 
			             Document.LocationToOffset (loc),
			             (int)mx,
			             (int)my);
		}
		
		void ShowTooltip (ModifierKeys modifierState, int offset, double xloc, double yloc)
		{
			CancelScheduledShow ();
			if (textEditorData.SuppressTooltips)
				return;
			if (tipWindow != null && currentTooltipProvider != null && currentTooltipProvider.IsInteractive (editor, tipWindow)) {
				double wx;
				var tipSize = tipWindow.Size;
				wx = tipX - tipSize.Width/2;
				if (xloc >= wx && xloc < wx + tipSize.Width && yloc >= tipY && yloc < tipY + 20 + tipSize.Height)
					return;
			}
			if (tipItem != null && !tipItem.ItemSegment.IsInvalid && !tipItem.ItemSegment.Contains (offset)) 
				HideTooltip ();
			nextTipX = xloc;
			nextTipY = yloc;
			nextTipOffset = offset;
			nextTipModifierState = modifierState;
			nextTipScheduledTime = DateTime.Now + TimeSpan.FromMilliseconds (TooltipTimeout);

			// If a tooltip is already scheduled, there is no need to create a new timer.
			if (tipShowTimeoutId == 0)
				tipShowTimeoutId = GLib.Timeout.Add (TooltipTimeout, TooltipTimer);
		}
		
		bool TooltipTimer ()
		{
			// This timer can't be reused, so reset the var now
			tipShowTimeoutId = 0;
			
			// Cancelled?
			if (nextTipOffset == -1)
				return false;
			
			int remainingMs = (int) (nextTipScheduledTime - DateTime.Now).TotalMilliseconds;
			if (remainingMs > 50) {
				// Still some significant time left. Re-schedule the timer
				tipShowTimeoutId = GLib.Timeout.Add ((uint) remainingMs, TooltipTimer);
				return false;
			}
			
			// Find a provider
			TooltipProvider provider = null;
			TooltipItem item = null;
			
			foreach (TooltipProvider tp in textEditorData.tooltipProviders) {
				try {
					item = tp.GetItem (editor, nextTipOffset);
				} catch (Exception e) {
					System.Console.WriteLine ("Exception in tooltip provider " + tp + " GetItem:");
					System.Console.WriteLine (e);
				}
				if (item != null) {
					provider = tp;
					break;
				}
			}
			
			if (item != null) {
				// Tip already being shown for this item?
				if (tipWindow != null && tipItem != null && tipItem.Equals (item)) {
					CancelScheduledHide ();
					return false;
				}
				
				tipX = nextTipX;
				tipY = nextTipY;
				tipItem = item;
				Window tw = null;
				try {
					tw = provider.ShowTooltipWindow (editor, nextTipOffset, nextTipModifierState, tipX + (int) TextViewMargin.XOffset, tipY, item);
				} catch (Exception e) {
					Console.WriteLine ("-------- Exception while creating tooltip:");
					Console.WriteLine (e);
				}
				if (tw == tipWindow)
					return false;
				HideTooltip ();
				if (tw == null)
					return false;
				
				CancelScheduledShow ();

				tipWindow = tw;
				currentTooltipProvider = provider;
				
				tipShowTimeoutId = 0;
			} else
				HideTooltip ();
			return false;
		}
		
		public void HideTooltip (bool checkMouseOver = true)
		{
			CancelScheduledHide ();
			CancelScheduledShow ();
			
			if (tipWindow != null) {
				if (checkMouseOver && tipWindow.GdkWindow != null) {
					// Don't hide the tooltip window if the mouse pointer is inside it.
					int x, y, w, h;
					Gdk.ModifierType m;
					tipWindow.GdkWindow.GetPointer (out x, out y, out m);
					tipWindow.GdkWindow.GetSize (out w, out h);
					if (x >= 0 && y >= 0 && x < w && y < h)
						return;
				}
				tipWindow.Destroy ();
				tipWindow = null;
			}
		}
		
		void DelayedHideTooltip ()
		{
			CancelScheduledHide ();
			tipHideTimeoutId = GLib.Timeout.Add (300, delegate {
				HideTooltip ();
				tipHideTimeoutId = 0;
				return false;
			});
		}
		
		void CancelScheduledHide ()
		{
			CancelScheduledShow ();
			if (tipHideTimeoutId != 0) {
				GLib.Source.Remove (tipHideTimeoutId);
				tipHideTimeoutId = 0;
			}
		}
		
		void CancelScheduledShow ()
		{
			// Don't remove the timeout handler since it may be reused
			nextTipOffset = -1;
		}
		
		void OnDocumentStateChanged (object s, EventArgs a)
		{
			HideTooltip ();
		}
		
		void OnTextSet (object sender, EventArgs e)
		{
			DocumentLine longest = longestLine;
			foreach (DocumentLine line in Document.Lines) {
				if (longest == null || line.Length > longest.Length)
					longest = line;
			}
			if (longest != longestLine) {
				int width = (int)(textViewMargin.GetLayout (longest).PangoWidth / Pango.Scale.PangoScale);
				
				if (width > this.longestLineWidth) {
					this.longestLineWidth = width;
					this.longestLine = longest;
				}
			}
		}
		#endregion
		
		#region Coordinate transformation
		public DocumentLocation PointToLocation (double xp, double yp, bool endAtEol = false)
		{
			return TextViewMargin.PointToLocation (xp, yp, endAtEol);
		}

		public DocumentLocation PointToLocation (Point p)
		{
			return TextViewMargin.PointToLocation (p);
		}

		public Point LocationToPoint (DocumentLocation loc)
		{
			return TextViewMargin.LocationToPoint (loc);
		}

		public Point LocationToPoint (int line, int column)
		{
			return TextViewMargin.LocationToPoint (line, column);
		}
		
		public Point LocationToPoint (int line, int column, bool useAbsoluteCoordinates)
		{
			return TextViewMargin.LocationToPoint (line, column, useAbsoluteCoordinates);
		}
		
		public Point LocationToPoint (DocumentLocation loc, bool useAbsoluteCoordinates)
		{
			return TextViewMargin.LocationToPoint (loc, useAbsoluteCoordinates);
		}

		public double ColumnToX (DocumentLine line, int column)
		{
			return TextViewMargin.ColumnToX (line, column);
		}
		
		/// <summary>
		/// Calculates the line number at line start (in one visual line could be several logical lines be displayed).
		/// </summary>
		public int YToLine (double yPos)
		{
			return TextViewMargin.YToLine (yPos);
		}
		
		public double LineToY (int logicalLine)
		{
			return TextViewMargin.LineToY (logicalLine);
		}
		
		public double GetLineHeight (DocumentLine line)
		{
			return TextViewMargin.GetLineHeight (line);
		}
		
		public double GetLineHeight (int logicalLineNumber)
		{
			return TextViewMargin.GetLineHeight (logicalLineNumber);
		}
		#endregion
		
		#region Animation
		Stage<Animation> animationStage = new Stage<Animation> ();
		List<Animation> actors = new List<Animation> ();
		
		protected void InitAnimations ()
		{
			animationStage.ActorStep += OnAnimationActorStep;
			animationStage.Iteration += OnAnimationIteration;
		}
		
		void DisposeAnimations ()
		{
			if (animationStage != null) {
				animationStage.Playing = false;
				animationStage.ActorStep -= OnAnimationActorStep;
				animationStage.Iteration -= OnAnimationIteration;
				animationStage = null;
			}
			
			if (actors != null) {
				foreach (Animation actor in actors) {
					if (actor is IDisposable)
						((IDisposable)actor).Dispose ();
				}
				actors.Clear ();
				actors = null;
			}
		}
		
		Animation StartAnimation (IAnimationDrawer drawer)
		{
			return StartAnimation (drawer, 300);
		}
		
		Animation StartAnimation (IAnimationDrawer drawer, uint duration)
		{
			return StartAnimation (drawer, duration, Easing.Linear);
		}
		
		Animation StartAnimation (IAnimationDrawer drawer, uint duration, Easing easing)
		{
			if (!Options.EnableAnimations)
				return null;
			Animation animation = new Animation (drawer, duration, easing, Blocking.Upstage);
			animationStage.Add (animation, duration);
			actors.Add (animation);
			return animation;
		}
		
		bool OnAnimationActorStep (Actor<Animation> actor)
		{
			switch (actor.Target.AnimationState) {
			case AnimationState.Coming:
				actor.Target.Drawer.Percent = actor.Percent;
				if (actor.Expired) {
					actor.Target.AnimationState = AnimationState.Going;
					actor.Reset ();
					return true;
				}
				break;
			case AnimationState.Going:
				if (actor.Expired) {
					RemoveAnimation (actor.Target);
					return false;
				}
				actor.Target.Drawer.Percent = 1.0 - actor.Percent;
				break;
			}
			return true;
		}
		
		void RemoveAnimation (Animation animation)
		{
			if (animation == null)
				return;
			Rectangle bounds = animation.Drawer.AnimationBounds;
			actors.Remove (animation);
			if (animation is IDisposable)
				((IDisposable)animation).Dispose ();
			QueueDrawArea (bounds.X, bounds.Y, bounds.Width, bounds.Height);
		}
		
		void OnAnimationIteration (object sender, EventArgs args)
		{
			foreach (Animation actor in actors) {
				Rectangle bounds = actor.Drawer.AnimationBounds;
				QueueDrawArea (bounds.X, bounds.Y, bounds.Width, bounds.Height);
			}
		}
		#endregion
		
		internal void FireLinkEvent (string link, uint button, ModifierKeys modifierState)
		{
			if (LinkRequest != null)
				LinkRequest (this, new LinkEventArgs (link, button, modifierState));
		}
		
		public event EventHandler<LinkEventArgs> LinkRequest;

		/// <summary>
		/// Inserts a margin at the specified list position
		/// </summary>
		public void InsertMargin (int index, Margin margin)
		{
			margins.Insert (index, margin);
			RedrawFromLine (0);
		}
		
		/// <summary>
		/// Checks whether the editor has a margin of a given type
		/// </summary>
		public bool HasMargin (Type marginType)
		{
			return margins.Exists((margin) => { return marginType.IsAssignableFrom (margin.GetType ()); });
		}
		
		/// <summary>
		/// Gets the first margin of a given type
		/// </summary>
		public Margin GetMargin (Type marginType)
		{
			return margins.Find((margin) => { return marginType.IsAssignableFrom (margin.GetType ()); });
		}
		bool requestResetCaretBlink = false;
		public void RequestResetCaretBlink ()
		{
			if (this.HasFocus)
				requestResetCaretBlink = true;
		}

		void UpdateLinesOnTextMarkerHeightChange (object sender, LineEventArgs e)
		{
			if (!e.Line.Markers.Any (m => m is IExtendingTextLineMarker))
				return;
			var line = e.Line.LineNumber;
			textEditorData.HeightTree.SetLineHeight (line, GetLineHeight (e.Line));
		}

		class SetCaret 
		{
			TextEditor view;
			int line, column;
			bool highlightCaretLine;
			bool centerCaret;
			
			public SetCaret (TextEditor view, int line, int column, bool highlightCaretLine, bool centerCaret)
			{
				this.view = view;
				this.line = line;
				this.column = column;
				this.highlightCaretLine = highlightCaretLine;
				this.centerCaret = centerCaret;
 			}
			
			public void Run (object sender, EventArgs e)
			{
				if (view.IsDisposed)
					return;
				line = System.Math.Min (line, view.Document.LineCount);
				view.Caret.AutoScrollToCaret = false;
				try {
					view.Caret.Location = new DocumentLocation (line, column);
					view.SetFocus ();
					if (centerCaret)
						view.CenterToCaret ();
					if (view.TextViewMargin.XOffset == 0)
						view.HAdjustment.Value = 0;
					view.TextArea.SizeAllocated -= Run;
				} finally {
					view.Caret.AutoScrollToCaret = true;
					if (highlightCaretLine) {
						view.TextViewMargin.HighlightCaretLine = true;
						view.StartCaretPulseAnimation ();
					}
				}
			}
		}

		public void SetCaretTo (int line, int column)
		{
			SetCaretTo (line, column, true);
		}
		
		public void SetCaretTo (int line, int column, bool highlight)
		{
			SetCaretTo (line, column, highlight, true);
		}

		public void SetCaretTo (int line, int column, bool highlight, bool centerCaret)
		{
			if (line < DocumentLocation.MinLine)
				throw new ArgumentException ("line < MinLine");
			if (column < DocumentLocation.MinColumn)
				throw new ArgumentException ("column < MinColumn");
			
			if (!sizeHasBeenAllocated) {
				SetCaret setCaret = new SetCaret (editor, line, column, highlight, centerCaret);
				SizeAllocated += setCaret.Run;
			} else {
				new SetCaret (editor, line, column, highlight, centerCaret).Run (null, null);
			}
		}

		#region Container
		/*public override ContainerChild this [Widget w] {
			get {
				return containerChildren.FirstOrDefault (info => info.Child == w || (info.Child is AnimatedWidget && ((AnimatedWidget)info.Child).Widget == w));
			}
		}

		public override GLib.GType ChildType ()
		{
			return Gtk.Widget.GType;
		}
		
		internal List<TextEditor.EditorContainerChild> containerChildren = new List<TextEditor.EditorContainerChild> ();
		
		public void AddTopLevelWidget (Gtk.Widget widget, int x, int y)
		{
			widget.Parent = this;
			TextEditor.EditorContainerChild info = new TextEditor.EditorContainerChild (this, widget);
			info.X = x;
			info.Y = y;
			containerChildren.Add (info);
			ResizeChild (Allocation, info);
			SetAdjustments ();
		}
		
		public void MoveTopLevelWidget (Gtk.Widget widget, int x, int y)
		{
			foreach (var info in containerChildren.ToArray ()) {
				if (info.Child == widget || (info.Child is AnimatedWidget && ((AnimatedWidget)info.Child).Widget == widget)) {
					if (info.X == x && info.Y == y)
						break;
					info.X = x;
					info.Y = y;
					if (widget.Visible)
						ResizeChild (Allocation, info);
					break;
				}
			}
			SetAdjustments ();
		}

		/// <summary>
		/// Returns the position of an embedded widget
		/// </summary>
		public void GetTopLevelWidgetPosition (Gtk.Widget widget, out int x, out int y)
		{
			foreach (var info in containerChildren.ToArray ()) {
				if (info.Child == widget || (info.Child is AnimatedWidget && ((AnimatedWidget)info.Child).Widget == widget)) {
					x = info.X;
					y = info.Y;
					return;
				}
			}
			x = y = 0;
		}
		
		public void MoveToTop (Gtk.Widget widget)
		{
			var editorContainerChild = containerChildren.FirstOrDefault (c => c.Child == widget);
			if (editorContainerChild == null)
				throw new Exception ("child " + widget + " not found.");
			var newChilds = containerChildren.Where (child => child != editorContainerChild).ToList ();
			newChilds.Add (editorContainerChild);
			this.containerChildren = newChilds;
			widget.GdkWindow.Raise ();
		}
		
		protected override void OnAdded (Widget widget)
		{
			AddTopLevelWidget (widget, 0, 0);
		}
		
		protected override void OnRemoved (Widget widget)
		{
			foreach (var info in containerChildren.ToArray ()) {
				if (info.Child == widget) {
					widget.Unparent ();
					containerChildren.Remove (info);
					SetAdjustments ();
					break;
				}
			}
		}
		
		protected override void ForAll (bool include_internals, Gtk.Callback callback)
		{
			containerChildren.ForEach (child => callback (child.Child));
		}
		
		protected override void OnMapped ()
		{
			WidgetFlags |= WidgetFlags.Mapped;
			// Note: SourceEditorWidget.ShowAutoSaveWarning() might have set TextEditor.Visible to false,
			// in which case we want to not map it (would cause a gtk+ critical error).
			containerChildren.ForEach (child => { if (child.Child.Visible) child.Child.Map (); });
			GdkWindow.Show ();
		}
		
		protected override void OnUnmapped ()
		{
			WidgetFlags &= ~WidgetFlags.Mapped;
			
			// We hide the window first so that the user doesn't see widgets disappearing one by one.
			GdkWindow.Hide ();
			
			containerChildren.ForEach (child => child.Child.Unmap ());
		}
		*/
		void ResizeChild (Rectangle allocation, Widget child)
		{
			var childRectangle = GetChildBounds(child);
			if (!child.FixedPosition) {
//				double zoom = Options.Zoom;
				childRectangle.X = (int)(child.X /** zoom */- HAdjustment.Value);
				childRectangle.Y = (int)(child.Y /** zoom */- VAdjustment.Value);
			}
			//			childRectangle.X += allocation.X;
			//			childRectangle.Y += allocation.Y;
			child.Child.SizeAllocate (childRectangle);
		}
		
		void SetChildrenPositions (Rectangle allocation)
		{
			foreach (var child in Children) {
				ResizeChild (allocation, child);
			}
		}
		#endregion

	}

	public interface ITextEditorDataProvider
	{
		TextEditorData GetTextEditorData ();
	}
}


