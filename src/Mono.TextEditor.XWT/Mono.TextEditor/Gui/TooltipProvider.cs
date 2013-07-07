//
// TooltipProvider.cs
//
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
//
// Copyright (c) 2012 Xamarin Inc
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using Xwt;

namespace Mono.TextEditor
{
	public abstract class TooltipProvider
	{
		public abstract TooltipItem GetItem (TextEditor editor, int offset);

		public virtual bool IsInteractive (TextEditor editor, Window tipWindow)
		{
			return false;
		}

		protected virtual void GetRequiredPosition (TextEditor editor, Window tipWindow, out int requiredWidth, out double xalign)
		{
			requiredWidth = tipWindow.Width;
			xalign = 0.5;
		}

		protected virtual Window CreateTooltipWindow (TextEditor editor, int offset, ModifierKeys modifierState, TooltipItem item)
		{
			return null;
		}

		public virtual Window ShowTooltipWindow (TextEditor editor, int offset, ModifierKeys modifierState, int mouseX, int mouseY, TooltipItem item)
		{
			Window tipWindow = CreateTooltipWindow (editor, offset, modifierState, item);
			if (tipWindow == null)
				return null;

			int w;
			double xalign;
			GetRequiredPosition (editor, tipWindow, out w, out xalign);
			w += 10;

			var loc = editor.ConvertToScreenCoordinates (new Point (mouseX, mouseY));
			/*int x = mouseX + ox + editor.Allocation.X;
			int y = mouseY + oy + editor.Allocation.Y;
			Gdk.Rectangle geometry = editor.Screen.GetUsableMonitorGeometry (editor.Screen.GetMonitorAtPoint (x, y));
			*/
			var geometry = editor.ScreenBounds;

			loc.X -= w * xalign;
			loc.Y += 10;
			
			if (loc.X + w >= geometry.X + geometry.Width)
				loc.X = geometry.X + geometry.Width - w;
			if (loc.X < geometry.Left)
				loc.X = geometry.Left;
			
			int h = tipWindow.Height;
			if (loc.Y + h >= geometry.Y + geometry.Height)
				loc.Y = geometry.Y + geometry.Height - h;
			if (loc.Y < geometry.Top)
				loc.Y = geometry.Top;
			
			tipWindow.Location = loc;
			
			tipWindow.Show ();

			return tipWindow;
		}
	}
}

