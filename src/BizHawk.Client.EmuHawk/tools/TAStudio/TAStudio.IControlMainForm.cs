﻿using BizHawk.Client.Common;

namespace BizHawk.Client.EmuHawk
{
	public partial class TAStudio : IControlMainform
	{
		private bool _suppressAskSave;

		public bool NamedStatePending { get; set; }

		public bool WantsToControlSavestates => !NamedStatePending;

		public void SaveState()
		{
			BookMarkControl.UpdateBranchExternal();
		}

		public void LoadState()
		{
			BookMarkControl.LoadBranchExternal();
		}

		public void SaveStateAs()
		{
			// dummy
		}

		public void LoadStateAs()
		{
			// dummy
		}

		public void SaveQuickSave(int slot)
		{
			BookMarkControl.UpdateBranchExternal(slot is 0 ? 9 : slot - 1);
		}

		public void LoadQuickSave(int slot)
		{
			BookMarkControl.LoadBranchExternal(slot is 0 ? 9 : slot - 1);
		}

		public bool SelectSlot(int slot)
		{
			BookMarkControl.SelectBranchExternal(slot is 0 ? 9 : slot - 1);
			return false;
		}

		public bool PreviousSlot()
		{
			BookMarkControl.SelectBranchExternal(false);
			return false;
		}

		public bool NextSlot()
		{
			BookMarkControl.SelectBranchExternal(true);
			return false;
		}

		public bool WantsToControlReadOnly => true;

		public void ToggleReadOnly()
		{
			if (CurrentTasMovie.IsPlayingOrFinished())
			{
				TastudioRecordMode();
			}
			else if (CurrentTasMovie.IsRecording())
			{
				TastudioPlayMode();
			}
		}

		public bool WantsToControlStopMovie { get; private set; }

		public void StopMovie(bool suppressSave)
		{
			if (!MainForm.GameIsClosing)
			{
				Focus();
				_suppressAskSave = suppressSave;
				NewTasMenuItem_Click(null, null);
				_suppressAskSave = false;
			}
		}

		public bool WantsToControlRewind => true;

		public void CaptureRewind()
		{
			// Do nothing, Tastudio handles this just fine
		}

		public bool Rewind()
		{
			// TODO: make this a config option.
			int step = MainForm.IsFastForwarding ? 16 : 2;
			// copy pasted from TasView_MouseWheel(), just without notch logic
			if (MainForm.IsSeeking && !MainForm.EmulatorPaused)
			{
				MainForm.PauseOnFrame -= step;

				// that's a weird condition here, but for whatever reason it works best
				if (Emulator.Frame >= MainForm.PauseOnFrame)
				{
					MainForm.PauseEmulator();
					StopSeeking();
					GoToFrame(Emulator.Frame - step);
				}

				RefreshDialog();
			}
			else
			{
				StopSeeking(); // late breaking memo: don't know whether this is needed
				GoToFrame(Emulator.Frame - step);
			}

			return true;
		}

		public bool WantsToControlRestartMovie { get; }

		public void RestartMovie()
		{
			if (AskSaveChanges())
			{
				WantsToControlStopMovie = false;
				StartNewMovieWrapper(CurrentTasMovie);
				WantsToControlStopMovie = true;
				RefreshDialog();
			}
		}

		public bool WantsToControlReboot { get; private set; } = true;

		public void RebootCore()
		{
			WantsToControlReboot = false;
			NewTasMenuItem_Click(null, null);
			WantsToControlReboot = true;
		}
	}
}
