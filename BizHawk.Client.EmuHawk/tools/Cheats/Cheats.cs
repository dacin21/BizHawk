﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using BizHawk.Emulation.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;
using BizHawk.Emulation.Cores.Nintendo.SNES;
using BizHawk.Emulation.Cores.Sega.Genesis;

using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk.ToolExtensions;
using BizHawk.Client.EmuHawk.WinFormExtensions;

namespace BizHawk.Client.EmuHawk
{
	public partial class Cheats : Form, IToolForm
	{
		private const string NAME = "NamesColumn";
		private const string ADDRESS = "AddressColumn";
		private const string VALUE = "ValueColumn";
		private const string COMPARE = "CompareColumn";
		private const string ON = "OnColumn";
		private const string DOMAIN = "DomainColumn";
		private const string SIZE = "SizeColumn";
		private const string ENDIAN = "EndianColumn";
		private const string TYPE = "DisplayTypeColumn";

		private int _defaultWidth;
		private int _defaultHeight;
		private string _sortedColumn = string.Empty;
		private bool _sortReverse;

		public Cheats()
		{
			InitializeComponent();
			Settings = new CheatsSettings();

			Closing += (o, e) =>
			{
				SaveConfigSettings();
			};

			CheatListView.QueryItemText += CheatListView_QueryItemText;
			CheatListView.QueryItemBkColor += CheatListView_QueryItemBkColor;
			CheatListView.VirtualMode = true;

			_sortedColumn = string.Empty;
			_sortReverse = false;
		}

		[RequiredService]
		private IMemoryDomains Core { get; set; }

		[ConfigPersist]
		public CheatsSettings Settings { get; set; }

		public bool UpdateBefore { get { return false; } }

		public void UpdateValues()
		{
			// Do nothing
		}

		public void FastUpdate()
		{
			// Do nothing
		}

		public void Restart()
		{
			CheatEditor.MemoryDomains = Core;
			CheatEditor.Restart();
		}

		/// <summary>
		/// Tools that want to refresh the cheats list should call this, not UpdateValues
		/// </summary>
		public void UpdateDialog()
		{
			CheatListView.ItemCount = Global.CheatList.Count;
			TotalLabel.Text = Global.CheatList.CheatCount
				+ (Global.CheatList.CheatCount == 1 ? " cheat " : " cheats ")
				+ Global.CheatList.ActiveCount + " active";
		}

		public void LoadFileFromRecent(string path)
		{
			var askResult = !Global.CheatList.Changes || AskSaveChanges();
			if (askResult)
			{
				var loadResult = Global.CheatList.Load(path, append: false);
				if (!loadResult)
				{
					Global.Config.RecentWatches.HandleLoadError(path);
				}
				else
				{
					Global.Config.RecentWatches.Add(path);
					UpdateDialog();
					UpdateMessageLabel();
				}
			}
		}

		private void UpdateMessageLabel(bool saved = false)
		{
			MessageLabel.Text = saved 
				? Path.GetFileName(Global.CheatList.CurrentFileName) + " saved."
				: Path.GetFileName(Global.CheatList.CurrentFileName) + (Global.CheatList.Changes ? " *" : string.Empty);
		}

		public bool AskSaveChanges()
		{
			return true;
		}

		private void LoadFile(FileSystemInfo file, bool append)
		{
			if (file != null)
			{
				var result = true;
				if (Global.CheatList.Changes)
				{
					result = AskSaveChanges();
				}

				if (result)
				{
					Global.CheatList.Load(file.FullName, append);
					UpdateDialog();
					UpdateMessageLabel();
					Global.Config.RecentCheats.Add(Global.CheatList.CurrentFileName);
				}
			}
		}

		private static bool SaveAs()
		{
			var file = ToolHelpers.SaveFileDialog(
				Global.CheatList.CurrentFileName,
				PathManager.GetCheatsPath(Global.Game),
				"Cheat Files",
				"cht");

			return file != null && Global.CheatList.SaveFile(file.FullName);
		}

		private void Cheats_Load(object sender, EventArgs e)
		{
			TopMost = Settings.TopMost;
			CheatEditor.MemoryDomains = Core;
			LoadConfigSettings();
			ToggleGameGenieButton();
			CheatEditor.SetAddEvent(AddCheat);
			CheatEditor.SetEditEvent(EditCheat);
			UpdateDialog();

			CheatsMenu.Items.Add(Settings.Columns.GenerateColumnsMenu(ColumnToggleCallback));
		}

		private void ColumnToggleCallback()
		{
			SaveColumnInfo();
			LoadColumnInfo();
		}

		private void ToggleGameGenieButton()
		{
			GameGenieToolbarSeparator.Visible =
				LoadGameGenieToolbarItem.Visible =
				GlobalWin.Tools.GameGenieAvailable;
		}

		private void AddCheat()
		{
			Global.CheatList.Add(CheatEditor.Cheat);
			UpdateDialog();
			UpdateMessageLabel();
		}

		private void EditCheat()
		{
			Global.CheatList.Exchange(CheatEditor.OriginalCheat, CheatEditor.Cheat);
			UpdateDialog();
			UpdateMessageLabel();
		}

		public void SaveConfigSettings()
		{
			SaveColumnInfo();

			if (WindowState == FormWindowState.Normal)
			{
				Settings.Wndx = Location.X;
				Settings.Wndy = Location.Y;
				Settings.Width = Right - Left;
				Settings.Height = Bottom - Top;
			}
		}

		private void LoadConfigSettings()
		{
			_defaultWidth = Size.Width;
			_defaultHeight = Size.Height;

			if (Settings.UseWindowPosition)
			{
				Location = Settings.WindowPosition;
			}

			if (Settings.UseWindowSize)
			{
				Size = Settings.WindowSize;
			}

			LoadColumnInfo();
		}

		private void LoadColumnInfo()
		{
			CheatListView.Columns.Clear();

			var columns = Settings.Columns
				.Where(c => c.Visible)
				.OrderBy(c => c.Index);

			foreach (var column in columns)
			{
				CheatListView.AddColumn(column);
			}
		}

		private void SaveColumnInfo()
		{
			foreach (ColumnHeader column in CheatListView.Columns)
			{
				Settings.Columns[column.Name].Index = column.DisplayIndex;
				Settings.Columns[column.Name].Width = column.Width;
			}
		}

		private void DoColumnToggle(string column)
		{
			Settings.Columns[column].Visible ^= true;
			SaveColumnInfo();
			LoadColumnInfo();
		}

		private void CheatListView_QueryItemText(int index, int column, out string text)
		{
			text = string.Empty;
			if (index >= Global.CheatList.Count || Global.CheatList[index].IsSeparator)
			{
				return;
			}

			var columnName = CheatListView.Columns[column].Name;

			switch (columnName)
			{
				case NAME:
					text = Global.CheatList[index].Name;
					break;
				case ADDRESS:
					text = Global.CheatList[index].AddressStr;
					break;
				case VALUE:
					text = Global.CheatList[index].ValueStr;
					break;
				case COMPARE:
					text = Global.CheatList[index].CompareStr;
					break;
				case ON:
					text = Global.CheatList[index].Enabled ? "*" : string.Empty;
					break;
				case DOMAIN:
					text = Global.CheatList[index].Domain.Name;
					break;
				case SIZE:
					text = Global.CheatList[index].Size.ToString();
					break;
				case ENDIAN:
					text = (Global.CheatList[index].BigEndian ?? false) ? "Big" : "Little";
					break;
				case TYPE:
					text = Watch.DisplayTypeToString(Global.CheatList[index].Type);
					break;
			}
		}

		private void CheatListView_QueryItemBkColor(int index, int column, ref Color color)
		{
			if (index < Global.CheatList.Count)
			{
				if (Global.CheatList[index].IsSeparator)
				{
					color = BackColor;
				}
				else if (Global.CheatList[index].Enabled)
				{
					color = Color.LightCyan;
				}
			}
		}

		private IEnumerable<int> SelectedIndices
		{
			get { return CheatListView.SelectedIndices.Cast<int>(); }
		}

		private IEnumerable<Cheat> SelectedItems
		{
			get { return SelectedIndices.Select(index => Global.CheatList[index]); }
		}

		private IEnumerable<Cheat> SelectedCheats
		{
			get { return SelectedItems.Where(x => !x.IsSeparator); }
		}

		private void DoSelectedIndexChange()
		{
			if (!CheatListView.SelectAllInProgress)
			{
				if (SelectedCheats.Any())
				{
					var cheat = SelectedCheats.First();
					CheatEditor.SetCheat(cheat);
					CheatGroupBox.Text = "Editing Cheat " + cheat.Name + " - " + cheat.AddressStr;
				}
				else
				{
					CheatEditor.ClearForm();
					CheatGroupBox.Text = "New Cheat";
				}
			}
		}

		private void StartNewList()
		{
			var result = !Global.CheatList.Changes || AskSaveChanges();
			if (result)
			{
				Global.CheatList.NewList(ToolManager.GenerateDefaultCheatFilename());
				UpdateDialog();
				UpdateMessageLabel();
				ToggleGameGenieButton();
			}
		}

		private void NewList()
		{
			var result = !Global.CheatList.Changes || AskSaveChanges();
			if (result)
			{
				StartNewList();
			}
		}

		private void RefreshFloatingWindowControl()
		{
			Owner = Settings.FloatingWindow ? null : GlobalWin.MainForm;
		}

		#region Events

		#region File

		private void FileSubMenu_DropDownOpened(object sender, EventArgs e)
		{
			SaveMenuItem.Enabled = Global.CheatList.Changes;
		}

		private void RecentSubMenu_DropDownOpened(object sender, EventArgs e)
		{
			RecentSubMenu.DropDownItems.Clear();
			RecentSubMenu.DropDownItems.AddRange(
				Global.Config.RecentCheats.RecentMenu(LoadFileFromRecent));
		}

		private void NewMenuItem_Click(object sender, EventArgs e)
		{
			NewList();
		}

		private void OpenMenuItem_Click(object sender, EventArgs e)
		{
			var append = sender == AppendMenuItem;
			var file = ToolHelpers.OpenFileDialog(
				Global.CheatList.CurrentFileName,
				PathManager.GetCheatsPath(Global.Game),
				"Cheat Files",
				"cht");

			LoadFile(file, append);
		}

		private void SaveMenuItem_Click(object sender, EventArgs e)
		{
			if (Global.CheatList.Changes)
			{
				if (Global.CheatList.Save())
				{
					UpdateMessageLabel(saved: true);
				}
			}
			else
			{
				SaveAsMenuItem_Click(sender, e);
			}
		}

		private void SaveAsMenuItem_Click(object sender, EventArgs e)
		{
			if (SaveAs())
			{
				UpdateMessageLabel(saved: true);
			}
		}

		private void ExitMenuItem_Click(object sender, EventArgs e)
		{
			Close();
		}

		#endregion

		#region Cheats

		private void CheatsSubMenu_DropDownOpened(object sender, EventArgs e)
		{
			RemoveCheatMenuItem.Enabled =
				MoveUpMenuItem.Enabled =
				MoveDownMenuItem.Enabled =
				ToggleMenuItem.Enabled =
				SelectedIndices.Any();

			DisableAllCheatsMenuItem.Enabled = Global.CheatList.ActiveCount > 0;

			GameGenieSeparator.Visible =
				OpenGameGenieEncoderDecoderMenuItem.Visible =
				GlobalWin.Tools.GameGenieAvailable;
		}

		private void RemoveCheatMenuItem_Click(object sender, EventArgs e)
		{
			var items = SelectedItems.ToList();
			if (items.Any())
			{
				foreach (var item in items)
				{
					Global.CheatList.Remove(item);
				}

				CheatListView.SelectedIndices.Clear();
				UpdateDialog();
			}
		}

		private void InsertSeparatorMenuItem_Click(object sender, EventArgs e)
		{
			if (SelectedIndices.Any())
			{
				Global.CheatList.Insert(SelectedIndices.Max(), Cheat.Separator);
			}
			else
			{
				Global.CheatList.Add(Cheat.Separator);
			}
			
			UpdateDialog();
			UpdateMessageLabel();
		}

		private void MoveUpMenuItem_Click(object sender, EventArgs e)
		{
			var indices = SelectedIndices.ToList();
			if (indices.Count == 0 || indices[0] == 0)
			{
				return;
			}

			foreach (var index in indices)
			{
				var cheat = Global.CheatList[index];
				Global.CheatList.Remove(cheat);
				Global.CheatList.Insert(index - 1, cheat);
			}

			var newindices = indices.Select(t => t - 1).ToList();

			CheatListView.SelectedIndices.Clear();
			foreach (var newi in newindices)
			{
				CheatListView.SelectItem(newi, true);
			}

			UpdateMessageLabel();
			UpdateDialog();
		}

		private void MoveDownMenuItem_Click(object sender, EventArgs e)
		{
			var indices = SelectedIndices.ToList();
			if (indices.Count == 0 || indices.Last() == Global.CheatList.Count - 1)
			{
				return;
			}

			for (var i = indices.Count - 1; i >= 0; i--)
			{
				var cheat = Global.CheatList[indices[i]];
				Global.CheatList.Remove(cheat);
				Global.CheatList.Insert(indices[i] + 1, cheat);
			}

			UpdateMessageLabel();

			var newindices = indices.Select(t => t + 1).ToList();

			CheatListView.SelectedIndices.Clear();
			foreach (var newi in newindices)
			{
				CheatListView.SelectItem(newi, true);
			}

			UpdateDialog();
		}

		private void SelectAllMenuItem_Click(object sender, EventArgs e)
		{
			CheatListView.SelectAll();
		}

		private void ToggleMenuItem_Click(object sender, EventArgs e)
		{
			SelectedCheats.ToList().ForEach(x => x.Toggle());
			CheatListView.Refresh();
		}

		private void DisableAllCheatsMenuItem_Click(object sender, EventArgs e)
		{
			Global.CheatList.DisableAll();
		}

		private void OpenGameGenieEncoderDecoderMenuItem_Click(object sender, EventArgs e)
		{
			GlobalWin.Tools.LoadGameGenieEc();
		}

		#endregion

		#region Options

		private void OptionsSubMenu_DropDownOpened(object sender, EventArgs e)
		{
			AlwaysLoadCheatsMenuItem.Checked = Global.Config.LoadCheatFileByGame;
			AutoSaveCheatsMenuItem.Checked = Global.Config.CheatsAutoSaveOnClose;
			DisableCheatsOnLoadMenuItem.Checked = Global.Config.DisableCheatsOnLoad;
			AutoloadMenuItem.Checked = Global.Config.RecentCheats.AutoLoad;
			SaveWindowPositionMenuItem.Checked = Settings.SaveWindowPosition;
			AlwaysOnTopMenuItem.Checked = Settings.TopMost;
			FloatingWindowMenuItem.Checked = Settings.FloatingWindow;
		}

		private void AlwaysLoadCheatsMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.LoadCheatFileByGame ^= true;
		}

		private void AutoSaveCheatsMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.CheatsAutoSaveOnClose ^= true;
		}

		private void CheatsOnOffLoadMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.DisableCheatsOnLoad ^= true;
		}

		private void AutoloadMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.RecentCheats.AutoLoad ^= true;
		}

		private void SaveWindowPositionMenuItem_Click(object sender, EventArgs e)
		{
			Settings.SaveWindowPosition ^= true;
		}

		private void AlwaysOnTopMenuItem_Click(object sender, EventArgs e)
		{
			Settings.TopMost ^= true;
		}

		private void FloatingWindowMenuItem_Click(object sender, EventArgs e)
		{
			Settings.FloatingWindow ^= true;
			RefreshFloatingWindowControl();
		}

		private void RestoreDefaultsMenuItem_Click(object sender, EventArgs e)
		{
			Size = new Size(_defaultWidth, _defaultHeight);
			Settings = new CheatsSettings();

			CheatsMenu.Items.Remove(
				CheatsMenu.Items
					.OfType<ToolStripMenuItem>()
					.First(x => x.Name == "GeneratedColumnsSubMenu")
			);

			CheatsMenu.Items.Add(Settings.Columns.GenerateColumnsMenu(ColumnToggleCallback));

			Global.Config.DisableCheatsOnLoad = false;
			Global.Config.LoadCheatFileByGame = true;
			Global.Config.CheatsAutoSaveOnClose = true;

			RefreshFloatingWindowControl();
			LoadColumnInfo();
		}

		#endregion

		#region ListView and Dialog Events

		private void CheatListView_Click(object sender, EventArgs e)
		{
		}

		private void CheatListView_DoubleClick(object sender, EventArgs e)
		{
			ToggleMenuItem_Click(sender, e);
		}

		private void CheatListView_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete && !e.Control && !e.Alt && !e.Shift)
			{
				RemoveCheatMenuItem_Click(sender, e);
			}
			else if (e.KeyCode == Keys.A && e.Control && !e.Alt && !e.Shift)
			{
				SelectAllMenuItem_Click(null, null);
			}
		}

		private void CheatListView_SelectedIndexChanged(object sender, EventArgs e)
		{
			DoSelectedIndexChange();
		}

		private void CheatListView_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			var column = CheatListView.Columns[e.Column];
			if (column.Name != _sortedColumn)
			{
				_sortReverse = false;
			}

			Global.CheatList.Sort(column.Name, _sortReverse);

			_sortedColumn = column.Name;
			_sortReverse ^= true;
			UpdateDialog();
		}

		private void NewCheatForm_DragDrop(object sender, DragEventArgs e)
		{
			var filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);
			if (Path.GetExtension(filePaths[0]) == ".cht")
			{
				LoadFile(new FileInfo(filePaths[0]), append: false);
				UpdateDialog();
				UpdateMessageLabel();
			}
		}

		private void NewCheatForm_DragEnter(object sender, DragEventArgs e)
		{
			e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
		}

		private void CheatsContextMenu_Opening(object sender, CancelEventArgs e)
		{
			ToggleContextMenuItem.Enabled =
				RemoveContextMenuItem.Enabled =
				SelectedCheats.Any();

			DisableAllContextMenuItem.Enabled = Global.CheatList.ActiveCount > 0;
		}

		private void ViewInHexEditorContextMenuItem_Click(object sender, EventArgs e)
		{
			var selected = SelectedCheats.ToList();
			if (selected.Any())
			{
				GlobalWin.Tools.Load<HexEditor>();

				if (selected.Select(x => x.Domain).Distinct().Count() > 1)
				{
					ToolHelpers.ViewInHexEditor(selected[0].Domain, new List<long> { selected.First().Address ?? 0 }, selected.First().Size);
				}
				else
				{
					ToolHelpers.ViewInHexEditor(selected.First().Domain, selected.Select(x => x.Address ?? 0), selected.First().Size);
				}
			}
		}

		protected override void OnShown(EventArgs e)
		{
			RefreshFloatingWindowControl();
			base.OnShown(e);
		}

		#endregion

		#endregion

		public class CheatsSettings : ToolDialogSettings
		{
			public CheatsSettings()
			{
				Columns = new ColumnList
				{
					new Column { Name = NAME, Visible = true, Index = 0, Width = 128 },
					new Column { Name = ADDRESS, Visible = true, Index = 1, Width = 60 },
					new Column { Name = VALUE, Visible = true, Index = 2, Width = 59 },
					new Column { Name = COMPARE, Visible = true, Index = 3, Width = 59 },
					new Column { Name = ON, Visible = false, Index = 4, Width = 28 },
					new Column { Name = DOMAIN, Visible = true, Index = 5, Width = 55 },
					new Column { Name = SIZE, Visible = true, Index = 6, Width = 55 },
					new Column { Name = ENDIAN, Visible = false, Index = 7, Width = 55 },
					new Column { Name = TYPE, Visible = false, Index = 8, Width = 55 }
				};
			}

			public ColumnList Columns { get; set; }
		}
	}
}
