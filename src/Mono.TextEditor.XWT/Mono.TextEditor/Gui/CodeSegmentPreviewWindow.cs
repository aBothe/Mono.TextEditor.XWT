//
// CodeSegmentPreviewWindow.cs
//
// Author:
//   Mike Krüger <mkrueger@novell.com>
//
// Copyright (C) 2008 Novell, Inc (http://www.novell.com)
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

using System;

using Xwt;
using Xwt.Drawing;
using System.Collections.Generic;

namespace Mono.TextEditor
{
	public class CodeSegmentPreviewWindow : Window
	{
		CodeSegmentPreviewCanvas canvas;
		TextEditor editor;

		
		public static string CodeSegmentPreviewInformString {
			get;
			set;
		}
		
		public bool HideCodeSegmentPreviewInformString {
			get {return canvas.HideCodeSegmentPreviewInformString;}
			private set{canvas.HideCodeSegmentPreviewInformString = value;}
		}
		
		public TextSegment Segment {
			get;
			private set;
		}
		
		public bool IsEmptyText {
			get {
				return canvas.IsEmptyText;
			}
		}

		public CodeSegmentPreviewWindow (TextEditor editor, bool hideCodeSegmentPreviewInformString, TextSegment segment, bool removeIndent = true) : this(editor, hideCodeSegmentPreviewInformString, segment, DefaultPreviewWindowWidth, DefaultPreviewWindowHeight, removeIndent)
		{
		}
		
		public CodeSegmentPreviewWindow (TextEditor editor, bool hideCodeSegmentPreviewInformString, TextSegment segment, int width, int height, bool removeIndent = true) 
		{
			canvas = new CodeSegmentPreviewCanvas (editor);
			Content = canvas;
			this.HideCodeSegmentPreviewInformString = hideCodeSegmentPreviewInformString;
			this.Segment = segment;
			this.editor = editor;
			//this.SkipPagerHint = this.SkipTaskbarHint = true;
			ShowInTaskbar = false;
			//this.TypeHint = WindowTypeHint.Menu;
					
			// setting a max size for the segment (40 lines should be enough), 
			// no need to markup thousands of lines for a preview window
			SetSegment (segment, removeIndent);
			CalculateSize (width);
		}
		
		const int maxLines = 40;
		
		public void SetSegment (TextSegment segment, bool removeIndent)
		{
			int startLine = editor.Document.OffsetToLineNumber (segment.Offset);
			int endLine = editor.Document.OffsetToLineNumber (segment.EndOffset);
			
			bool pushedLineLimit = endLine - startLine > maxLines;
			if (pushedLineLimit)
				segment = new TextSegment (segment.Offset, editor.Document.GetLine (startLine + maxLines).Offset - segment.Offset);
			canvas.layout.Trimming = TextTrimming.WordElipsis;
			//canvas.layout.Ellipsize = Pango.EllipsizeMode.End;
			canvas.layout.Markup = editor.GetTextEditorData ().GetMarkup (segment.Offset, segment.Length, removeIndent) + (pushedLineLimit ? Environment.NewLine + "..." : "");
			canvas.QueueDraw ();
		}
		
		public int PreviewInformStringHeight {
			get {return canvas.PreviewInformStringHeight; }
		}
		
		public void CalculateSize (int defaultWidth = -1)
		{
			canvas.CalculateSize (defaultWidth);
		}
		
		protected override void Dispose (bool disposing)
		{
			canvas.Dispose ();
			base.Dispose (disposing);
		}

		/*protected override bool OnKeyPressEvent (EventKey evnt)
		{
//			Console.WriteLine (evnt.Key);
			return base.OnKeyPressEvent (evnt);
		}*/
	}

	class CodeSegmentPreviewCanvas : Canvas
	{
		#region Properties
		Font fontDescription;
		internal TextLayout layout;
		TextLayout informLayout;
		const int DefaultPreviewWindowWidth = 320;
		const int DefaultPreviewWindowHeight = 200;

		public int PreviewInformStringHeight {
			get; private set;
		}


		public readonly TextEditor Editor;

		Color textGC, foldGC, textBgGC, foldBgGC;

		public bool IsEmptyText {
			get {
				return string.IsNullOrEmpty ((layout.Text ?? "").Trim ());
			}
		}

		public bool HideCodeSegmentPreviewInformString {
			get;
			set;
		}
		#endregion

		public CodeSegmentPreviewCanvas(TextEditor editor)
		{
			this.Editor = editor;

			layout = new TextLayout (this);
			informLayout = new TextLayout (this);
			informLayout.Text = CodeSegmentPreviewWindow.CodeSegmentPreviewInformString;

			fontDescription = Font.FromName (editor.Options.FontName).WithScaledSize(0.8);
			layout.Font = fontDescription;
			layout.Trimming = TextTrimming.WordElipsis;
			//layout.Ellipsize = Pango.EllipsizeMode.End;
		}

		public void CalculateSize (int defaultWidth = -1)
		{
			var sz = layout.GetSize ();
			var h = sz.Height, w = sz.Width;

			if (!HideCodeSegmentPreviewInformString) {
				sz = informLayout.GetSize ();
				PreviewInformStringHeight = sz.Height;
				w = System.Math.Max (w, sz.Width);
				h += sz.Height;
			}
			var geometry = ParentWindow.Screen.VisibleBounds;
			WidthRequest = System.Math.Max (1, System.Math.Min (w + 3, geometry.Width * 2 / 5));
			HeightRequest= System.Math.Max (1, System.Math.Min (h + 3, geometry.Height * 2 / 5));
		}

		protected override void OnDraw (Context ctx, Rectangle dirtyRect)
		{
			if (textGC == null) {
				textGC = Editor.ColorStyle.PlainText.Foreground;
				textBgGC = Editor.ColorStyle.PlainText.Background;
				foldGC = Editor.ColorStyle.CollapsedText.Foreground;
				foldBgGC = Editor.ColorStyle.CollapsedText.Background;
			}

			ctx.SetColor (textBgGC);
			ctx.Rectangle (dirtyRect);
			ctx.SetColor(textGC);
			ctx.DrawTextLayout (layout, 1, 1);
			var sz = Size;
			ctx.SetColor (textBgGC);
			ctx.Rectangle (1, 1, sz.Width - 3, sz.Height - 3);
			ctx.SetColor (foldGC);
			ctx.Rectangle (0, 0, sz.Width - 1, sz.Height - 1);

			if (!HideCodeSegmentPreviewInformString) {
				informLayout.Text = CodeSegmentPreviewWindow.CodeSegmentPreviewInformString;
				var layoutSize = informLayout.GetSize ();
				var h = layoutSize.Height, w = layoutSize.Width;
				PreviewInformStringHeight = h;
				ctx.SetColor (foldBgGC);
				ctx.Rectangle (sz.Width - w - 3, sz.Height - h, w + 2, h - 1);
				ctx.SetColor (foldGC);
				ctx.DrawTextLayout (informLayout,sz.Width - w - 3, sz.Height - h);
			}
		}

		protected override void Dispose (bool disposing)
		{
			layout = layout.Kill ();
			informLayout = informLayout.Kill ();
			fontDescription = fontDescription.Kill ();
			if (textGC != null) {
				textGC = textBgGC = foldGC = foldBgGC = null;
			}
			base.Dispose (disposing);
		}
	}
}
