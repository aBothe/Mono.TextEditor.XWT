//
// Program.cs
//
// Author:
//       Alexander Bothe <info@alexanderbothe.com>
//
// Copyright (c) 2013 Alexander Bothe
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
using Xwt.Drawing;

namespace XwtExperiments
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Application.Initialize ();

			var w = new Xwt.Window ();
			w.Padding = 0;
			w.Title = "Teststestse";
			w.Hidden+=(object s, EventArgs ea) => {
				Application.Exit();
				w.Dispose();
			};

			var vb = new VBox ();
			w.Content = vb;

			var mc = new MyMainComponent ();
			mc.LineColor = Colors.Green;
			mc.ExpandHorizontal = true;
			vb.PackStart (mc);

			var scr = new HScrollbar ();
			scr.ExpandHorizontal = true;
			vb.PackEnd (scr);

			scr.LowerValue = 0;
			scr.UpperValue = 1;
			scr.PageSize = 0.05;
			scr.PageIncrement = 0.05;


			mc = new MyMainComponent { LineColor = Colors.Blue, WidthRequest = 400, HeightRequest = 40 };
			vb.PackEnd (mc);
			mc.ExpandHorizontal = true;
			mc.ExpandVertical = true;

			scr.ValueChanged += (object o, EventArgs ea) =>
			{
				mc.modValue = scr.Value;
				mc.QueueDraw();
			};

			var button = new Button ("Hello");
			mc.AddChild (button, 50, 20);

			w.Show ();



			Application.Run ();
			Application.Dispose ();
		}
	}

	class MyMainComponent : Canvas
	{
		Color lineCol, drawnCol;
		public double modValue=1;
		Menu cm;
		public Color LineColor{
			get{
				return lineCol;
			}
			set{
				lineCol = value;
				drawnCol = value;
			}
		}

		/*
		protected override Size OnGetPreferredSize (SizeConstraint widthConstraint, SizeConstraint heightConstraint)
		{
			return new Size (300, 500);
		}*/

		public MyMainComponent()
		{
			base.CanGetFocus = true;

			cm = new Menu ();
			cm.Items.Add (new MenuItem("Morning!"));
		}

		protected override void OnMouseEntered (EventArgs args)
		{
			base.OnMouseEntered (args);
			SetFocus ();
		}

		protected override void OnKeyPressed (KeyEventArgs args)
		{
			drawnCol = Colors.Red;
			var ch = (char) args.Key;
			base.OnKeyPressed (args);
			QueueDraw ();
		}

		protected override void OnKeyReleased (KeyEventArgs args)
		{
			drawnCol = lineCol;
			base.OnKeyReleased (args);
			QueueDraw ();
		}

		protected override void OnButtonPressed (ButtonEventArgs args)
		{
			if (args.Button == PointerButton.Right) {
				cm.Popup(this, args.X, args.Y);
			}

			base.OnButtonPressed (args);
		}

		protected override void OnMouseMoved (MouseMovedEventArgs args)
		{
			ParentWindow.Title = args.X + "; "+ args.Y;
			base.OnMouseMoved (args);
		}

		protected override void OnDraw (Context ctx, Rectangle dirtyRect)
		{
			var sz = Size;
			ctx.SetColor (drawnCol);
			ctx.SetLineWidth (5);
			
			ctx.Rectangle (4, 5, 60, 60);
			//ctx.Clip ();
			ctx.MoveTo (0, 0);
			ctx.LineTo (Size.Width * modValue, Size.Height);
			ctx.Stroke ();
			//base.OnDraw (ctx, dirtyRect);
		}
	}
}
