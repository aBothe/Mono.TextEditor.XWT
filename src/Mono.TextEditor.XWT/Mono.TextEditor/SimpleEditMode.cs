//
// SimpleEditMode.cs
//
// Author:
//   Michael Hutchinson <mhutchinson@novell.com>
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
using System.Collections.Generic;
using Xwt;

namespace Mono.TextEditor
{
	public class SimpleEditMode : EditMode
	{
		Dictionary<ulong, Action<TextEditorData>> keyBindings = new Dictionary<ulong, Action<TextEditorData>> ();
		public Dictionary<int, Action<TextEditorData>> KeyBindings { get { return keyBindings; } }
		
		public SimpleEditMode ()
		{
			if (Platform.IsMac)
				InitMacBindings ();
			else
				InitDefaultBindings ();
		}
		
		void InitCommonBindings ()
		{
			Action<TextEditorData> action;

			//TODO: What is the Mod1 key? I've currently chosen ModifierKeys.Command instead of Gdk.ModifierType.Mod1Mask
			ModifierKeys wordModifier = Platform.IsMac? ModifierKeys.Command : ModifierKeys.Control;
			ModifierKeys subwordModifier = Platform.IsMac? ModifierKeys.Control : ModifierKeys.Alt;
						
			// ==== Left ====
			
			action = CaretMoveActions.Left;
			keyBindings.Add (GetKeyCode (Key.NumPadLeft), action);
			keyBindings.Add (GetKeyCode (Key.Left), action);
			
			action = SelectionActions.MoveLeft;
			keyBindings.Add (GetKeyCode (Key.NumPadLeft, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Left, ModifierKeys.Shift), action);
			
			action = CaretMoveActions.PreviousWord;
			keyBindings.Add (GetKeyCode (Key.NumPadLeft, wordModifier), action);
			keyBindings.Add (GetKeyCode (Key.Left, wordModifier), action);
			
			action = SelectionActions.MovePreviousWord;
			keyBindings.Add (GetKeyCode (Key.NumPadLeft, ModifierKeys.Shift | wordModifier), action);
			keyBindings.Add (GetKeyCode (Key.Left, ModifierKeys.Shift | wordModifier), action);
			
			// ==== Right ====
			
			action = CaretMoveActions.Right;
			keyBindings.Add (GetKeyCode (Key.NumPadRight), action);
			keyBindings.Add (GetKeyCode (Key.Right), action);
			
			action = SelectionActions.MoveRight;
			keyBindings.Add (GetKeyCode (Key.NumPadRight, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Right, ModifierKeys.Shift), action);
			
			action = CaretMoveActions.NextWord;
			keyBindings.Add (GetKeyCode (Key.NumPadRight, wordModifier), action);
			keyBindings.Add (GetKeyCode (Key.Right, wordModifier), action);
			
			action = SelectionActions.MoveNextWord;
			keyBindings.Add (GetKeyCode (Key.NumPadRight, ModifierKeys.Shift | wordModifier), action);
			keyBindings.Add (GetKeyCode (Key.Right, ModifierKeys.Shift | wordModifier), action);
			
			// ==== Up ====
			
			action = CaretMoveActions.Up;
			keyBindings.Add (GetKeyCode (Key.NumPadUp), action);
			keyBindings.Add (GetKeyCode (Key.Up), action);
			
			action = ScrollActions.Up;
			keyBindings.Add (GetKeyCode (Key.NumPadUp, ModifierKeys.Control), action);
			keyBindings.Add (GetKeyCode (Key.Up, ModifierKeys.Control), action);
			
			// ==== Down ====
			
			action = CaretMoveActions.Down;
			keyBindings.Add (GetKeyCode (Key.NumPadDown), action);
			keyBindings.Add (GetKeyCode (Key.Down), action);
			
			action = ScrollActions.Down;
			keyBindings.Add (GetKeyCode (Key.NumPadDown, ModifierKeys.Control), action);
			keyBindings.Add (GetKeyCode (Key.Down, ModifierKeys.Control), action);
			
			// ==== Deletion, insertion ====
			
			action = MiscActions.SwitchCaretMode;
			keyBindings.Add (GetKeyCode (Key.NumPadInsert), action);
			keyBindings.Add (GetKeyCode (Key.Insert), action);
			
			keyBindings.Add (GetKeyCode (Key.Tab), MiscActions.InsertTab);
			keyBindings.Add (GetKeyCode (Key.Tab, ModifierKeys.Shift), MiscActions.RemoveTab);
			
			action = MiscActions.InsertNewLine;
			keyBindings.Add (GetKeyCode (Key.Return), action);
			keyBindings.Add (GetKeyCode (Key.NumPadEnter), action);
			
			keyBindings.Add (GetKeyCode (Key.Return, ModifierKeys.Control), MiscActions.InsertNewLinePreserveCaretPosition);
			keyBindings.Add (GetKeyCode (Key.Return, ModifierKeys.Shift), MiscActions.InsertNewLineAtEnd);
			
			action = DeleteActions.Backspace;
			keyBindings.Add (GetKeyCode (Key.BackSpace), action);
			keyBindings.Add (GetKeyCode (Key.BackSpace, ModifierKeys.Shift), action);
			
			keyBindings.Add (GetKeyCode (Key.BackSpace, wordModifier), DeleteActions.PreviousWord);
			
			action = DeleteActions.Delete;
			keyBindings.Add (GetKeyCode (Key.NumPadDelete), action);
			keyBindings.Add (GetKeyCode (Key.Delete), action);
			
			action = DeleteActions.NextWord;
			keyBindings.Add (GetKeyCode (Key.NumPadDelete, wordModifier), action);
			keyBindings.Add (GetKeyCode (Key.Delete, wordModifier), action);
			
			
			// == subword motions ==
						
			action = CaretMoveActions.PreviousSubword;
			keyBindings.Add (GetKeyCode (Key.NumPadLeft, subwordModifier), action);
			keyBindings.Add (GetKeyCode (Key.Left, subwordModifier), action);
			
			action = SelectionActions.MovePreviousSubword;
			keyBindings.Add (GetKeyCode (Key.NumPadLeft, ModifierKeys.Shift | subwordModifier), action);
			keyBindings.Add (GetKeyCode (Key.Left, ModifierKeys.Shift | subwordModifier), action);
			
			action = CaretMoveActions.NextSubword;
			keyBindings.Add (GetKeyCode (Key.NumPadRight, subwordModifier), action);
			keyBindings.Add (GetKeyCode (Key.Right, subwordModifier), action);
			
			action = SelectionActions.MoveNextSubword;
			keyBindings.Add (GetKeyCode (Key.NumPadRight, ModifierKeys.Shift | subwordModifier), action);
			keyBindings.Add (GetKeyCode (Key.Right, ModifierKeys.Shift | subwordModifier), action);
			
			keyBindings.Add (GetKeyCode (Key.BackSpace, subwordModifier), DeleteActions.PreviousSubword);
			
			action = DeleteActions.NextSubword;
			keyBindings.Add (GetKeyCode (Key.NumPadDelete, subwordModifier), action);
			keyBindings.Add (GetKeyCode (Key.Delete, subwordModifier), action);
		}
		
		void InitDefaultBindings ()
		{
			InitCommonBindings ();
			
			Action<TextEditorData> action;
			
			// === Home ===
			
			action = CaretMoveActions.LineHome;
			keyBindings.Add (GetKeyCode (Key.NumPadHome), action);
			keyBindings.Add (GetKeyCode (Key.Home), action);
			
			action = SelectionActions.MoveLineHome;
			keyBindings.Add (GetKeyCode (Key.NumPadHome, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Home, ModifierKeys.Shift), action);
			
			action = CaretMoveActions.ToDocumentStart;
			keyBindings.Add (GetKeyCode (Key.NumPadHome, ModifierKeys.Control), action);
			keyBindings.Add (GetKeyCode (Key.Home, ModifierKeys.Control), action);
			
			action = SelectionActions.MoveToDocumentStart;
			keyBindings.Add (GetKeyCode (Key.NumPadHome, ModifierKeys.Shift | ModifierKeys.Control), action);
			keyBindings.Add (GetKeyCode (Key.Home, ModifierKeys.Shift | ModifierKeys.Control), action);
			
			// ==== End ====
			
			action = CaretMoveActions.LineEnd;
			keyBindings.Add (GetKeyCode (Key.NumPadEnd), action);
			keyBindings.Add (GetKeyCode (Key.End), action);
			
			action = SelectionActions.MoveLineEnd;
			keyBindings.Add (GetKeyCode (Key.NumPadEnd, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.End, ModifierKeys.Shift), action);
			
			action = CaretMoveActions.ToDocumentEnd;
			keyBindings.Add (GetKeyCode (Key.NumPadEnd, ModifierKeys.Control), action);
			keyBindings.Add (GetKeyCode (Key.End, ModifierKeys.Control), action);
			
			action = SelectionActions.MoveToDocumentEnd;
			keyBindings.Add (GetKeyCode (Key.NumPadEnd, ModifierKeys.Shift | ModifierKeys.Control), action);
			keyBindings.Add (GetKeyCode (Key.End, ModifierKeys.Shift | ModifierKeys.Control), action);
			
			// ==== Cut, copy, paste ===
			
			action = ClipboardActions.Cut;
			keyBindings.Add (GetKeyCode (Key.NumPadDelete, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Delete, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.x, ModifierKeys.Control), action);
			
			action = ClipboardActions.Copy;
			keyBindings.Add (GetKeyCode (Key.NumPadInsert, ModifierKeys.Control), action);
			keyBindings.Add (GetKeyCode (Key.Insert, ModifierKeys.Control), action);
			keyBindings.Add (GetKeyCode (Key.c, ModifierKeys.Control), action);
			
			action = ClipboardActions.Paste;
			keyBindings.Add (GetKeyCode (Key.NumPadInsert, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Insert, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.v, ModifierKeys.Control), action);
			
			// ==== Page up/down ====
			
			action = CaretMoveActions.PageUp;
			//keyBindings.Add (GetKeyCode (Key.NumPadPage_Up, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.PageUp), action);
			
			action = SelectionActions.MovePageUp;
			//keyBindings.Add (GetKeyCode (Key.NumPadPage_Up, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.PageUp, ModifierKeys.Shift), action);
			
			action = CaretMoveActions.PageDown;
			//keyBindings.Add (GetKeyCode (Key.NumPadPage_Down), action);
			keyBindings.Add (GetKeyCode (Key.PageDown), action);
			
			action = SelectionActions.MovePageDown;
			//keyBindings.Add (GetKeyCode (Key.NumPadPage_Down, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.PageDown, ModifierKeys.Shift), action);
			
			// ==== Misc ====
			
			keyBindings.Add (GetKeyCode (Key.a, ModifierKeys.Control), SelectionActions.SelectAll);
			
			keyBindings.Add (GetKeyCode (Key.d, ModifierKeys.Control), DeleteActions.CaretLine);
			keyBindings.Add (GetKeyCode (Key.D, ModifierKeys.Shift | ModifierKeys.Control), DeleteActions.CaretLineToEnd);
			
			keyBindings.Add (GetKeyCode (Key.z, ModifierKeys.Control), MiscActions.Undo);
			keyBindings.Add (GetKeyCode (Key.z, ModifierKeys.Control | ModifierKeys.Shift), MiscActions.Redo);
			
			keyBindings.Add (GetKeyCode (Key.F2), BookmarkActions.GotoNext);
			keyBindings.Add (GetKeyCode (Key.F2, ModifierKeys.Shift), BookmarkActions.GotoPrevious);
			
			keyBindings.Add (GetKeyCode (Key.b, ModifierKeys.Control), MiscActions.GotoMatchingBracket);
			
			//Non-mac selection actions
			
			action = SelectionActions.MoveDown;
			keyBindings.Add (GetKeyCode (Key.NumPadDown, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Down, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Down, ModifierKeys.Shift | ModifierKeys.Control), action);
			
			action = SelectionActions.MoveUp;
			keyBindings.Add (GetKeyCode (Key.NumPadUp, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Up, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Up, ModifierKeys.Shift | ModifierKeys.Control), action);
		}
		
		void InitMacBindings ()
		{
			InitCommonBindings ();
			
			Action<TextEditorData> action;
			
			// Up/down
			action = CaretMoveActions.UpLineStart;
			keyBindings.Add (GetKeyCode (Key.Up, ModifierKeys.Command), action);
			
			action = CaretMoveActions.DownLineEnd;
			keyBindings.Add (GetKeyCode (Key.Down, ModifierKeys.Command), action);
			
			action = SelectionActions.MoveUpLineStart;
			keyBindings.Add (GetKeyCode (Key.Up, ModifierKeys.Command | ModifierKeys.Shift), action);
			
			action = SelectionActions.MoveDownLineEnd;
			keyBindings.Add (GetKeyCode (Key.Down, ModifierKeys.Command | ModifierKeys.Shift), action);
				
			// === Home ===
			
			action = CaretMoveActions.LineHome;
			keyBindings.Add (GetKeyCode (Key.Left, ModifierKeys.Alt), action);
			keyBindings.Add (GetKeyCode (Key.a, ModifierKeys.Control), action); //emacs
			keyBindings.Add (GetKeyCode (Key.a, ModifierKeys.Control | ModifierKeys.Shift), SelectionActions.MoveLineHome);
			
			action = SelectionActions.MoveLineHome;
			keyBindings.Add (GetKeyCode (Key.Left, ModifierKeys.Alt | ModifierKeys.Shift), action);
			
			action = CaretMoveActions.ToDocumentStart;
			keyBindings.Add (GetKeyCode (Key.NumPadHome), action);
			keyBindings.Add (GetKeyCode (Key.Home), action);
			keyBindings.Add (GetKeyCode (Key.Up, ModifierKeys.Alt), action);

			action = SelectionActions.MoveToDocumentStart;
			keyBindings.Add (GetKeyCode (Key.NumPadHome, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Home, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Up, ModifierKeys.Alt | ModifierKeys.Shift), action);

			// ==== End ====
			
			action = CaretMoveActions.LineEnd;
			keyBindings.Add (GetKeyCode (Key.Right, ModifierKeys.Alt), action);
			keyBindings.Add (GetKeyCode (Key.e, ModifierKeys.Control), action); //emacs
			keyBindings.Add (GetKeyCode (Key.e, ModifierKeys.Control | ModifierKeys.Shift), SelectionActions.MoveLineEnd);
			
			
			action = SelectionActions.MoveLineEnd;
			keyBindings.Add (GetKeyCode (Key.Right, ModifierKeys.Alt | ModifierKeys.Shift), action);
			
			action = CaretMoveActions.ToDocumentEnd;
			keyBindings.Add (GetKeyCode (Key.NumPadEnd), action);
			keyBindings.Add (GetKeyCode (Key.End), action);
			keyBindings.Add (GetKeyCode (Key.Down, ModifierKeys.Alt), action);

			action = SelectionActions.MoveToDocumentEnd;
			keyBindings.Add (GetKeyCode (Key.NumPadEnd, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.End, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Down, ModifierKeys.Alt | ModifierKeys.Shift), action);

			// ==== Cut, copy, paste ===
			
			action = ClipboardActions.Cut;
			keyBindings.Add (GetKeyCode (Key.x, ModifierKeys.Alt), action);
			keyBindings.Add (GetKeyCode (Key.w, ModifierKeys.Control), action); //emacs
			
			action = ClipboardActions.Copy;
			keyBindings.Add (GetKeyCode (Key.c, ModifierKeys.Alt), action);
			
			action = ClipboardActions.Paste;
			keyBindings.Add (GetKeyCode (Key.v, ModifierKeys.Alt), action);
			keyBindings.Add (GetKeyCode (Key.y, ModifierKeys.Control), action); //emacs
			
			// ==== Page up/down ====
			
			action = ScrollActions.PageDown;
			//keyBindings.Add (GetKeyCode (Key.NumPadPage_Down), action);
			keyBindings.Add (GetKeyCode (Key.PageDown), action);
			
			action = ScrollActions.PageUp;
			//keyBindings.Add (GetKeyCode (Key.NumPadPage_Up), action);
			keyBindings.Add (GetKeyCode (Key.PageUp), action);
			
			action = CaretMoveActions.PageDown;
			//keyBindings.Add (GetKeyCode (Key.NumPadPage_Down, Gdk.ModifierType.Mod1Mask), action);
			keyBindings.Add (GetKeyCode (Key.PageDown, ModifierKeys.Command), action);
			
			action = CaretMoveActions.PageUp;
			//keyBindings.Add (GetKeyCode (Key.NumPadPage_Up, Gdk.ModifierType.Mod1Mask), action);
			keyBindings.Add (GetKeyCode (Key.PageUp, ModifierKeys.Command), action);
			
			action = SelectionActions.MovePageUp;
			//keyBindings.Add (GetKeyCode (Key.NumPadPage_Up, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.PageUp, ModifierKeys.Shift), action);
			
			action = SelectionActions.MovePageDown;
			//keyBindings.Add (GetKeyCode (Key.NumPadPage_Down, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.PageDown, ModifierKeys.Shift), action);
			
			// ==== Misc ====
			
			keyBindings.Add (GetKeyCode (Key.a, ModifierKeys.Alt), SelectionActions.SelectAll);
			
			keyBindings.Add (GetKeyCode (Key.z, ModifierKeys.Alt), MiscActions.Undo);
			keyBindings.Add (GetKeyCode (Key.z, ModifierKeys.Alt | ModifierKeys.Shift), MiscActions.Redo);

			// selection actions
			
			action = SelectionActions.MoveDown;
			keyBindings.Add (GetKeyCode (Key.NumPadDown, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Down, ModifierKeys.Shift), action);
			
			action = SelectionActions.MoveUp;
			keyBindings.Add (GetKeyCode (Key.NumPadUp, ModifierKeys.Shift), action);
			keyBindings.Add (GetKeyCode (Key.Up, ModifierKeys.Shift), action);
			
			// extra emacs stuff
			keyBindings.Add (GetKeyCode (Key.f, ModifierKeys.Control), CaretMoveActions.Right);
			keyBindings.Add (GetKeyCode (Key.b, ModifierKeys.Control), CaretMoveActions.Left);
			keyBindings.Add (GetKeyCode (Key.p, ModifierKeys.Control), CaretMoveActions.Up);
			keyBindings.Add (GetKeyCode (Key.n, ModifierKeys.Control), CaretMoveActions.Down);
			keyBindings.Add (GetKeyCode (Key.h, ModifierKeys.Control), DeleteActions.Backspace);
			keyBindings.Add (GetKeyCode (Key.d, ModifierKeys.Control), DeleteActions.Delete);
			keyBindings.Add (GetKeyCode (Key.o, ModifierKeys.Control), MiscActions.InsertNewLinePreserveCaretPosition);
		}
		
		public void AddBinding (Key key, Action<TextEditorData> action)
		{
			keyBindings.Add (GetKeyCode (key), action);
		}

		public override void SelectValidShortcut (Tuple<Key,ModifierKeys>[] accels, out Key key, out ModifierKeys mod)
		{
			foreach (var accel in accels) {
				var keyCode = GetKeyCode (accel.Item1, accel.Item2);
				if (keyBindings.ContainsKey (keyCode)) {
					key = accel.Item1;
					mod = accel.Item2;
					return;
				}
			}
			key = accels [0].Item1;
			mod = accels [0].Item2;
		}

		
		protected override void HandleKeypress (Key key, uint unicodeKey, ModifierKeys modifier)
		{
			var keyCode = GetKeyCode (key, modifier);
			if (keyBindings.ContainsKey (keyCode)) {
				RunAction (keyBindings [keyCode]);
			} else if (unicodeKey != 0 && modifier == ModifierKeys.None) {
				InsertCharacter (unicodeKey);
			}
		}
	}
}
