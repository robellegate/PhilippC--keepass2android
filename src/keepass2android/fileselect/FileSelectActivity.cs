/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Content.PM;
using Android.Views.InputMethods;
using System.IO;
using KeePassLib.Serialization;

namespace keepass2android
{

	[Activity (Label = "@string/app_name", 
	           ConfigurationChanges=ConfigChanges.Orientation|
	           ConfigChanges.KeyboardHidden, 
	           Theme="@style/Base")]
	[IntentFilter(new [] { Intent.ActionSend }, 
		Label = "@string/kp2a_findUrl", 
		Categories=new[]{Intent.CategoryDefault}, 
		DataMimeType="text/plain")]
	public class FileSelectActivity : ListActivity
	{

		public FileSelectActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}
		public FileSelectActivity()
		{
		}

		private const int CMENU_CLEAR = Menu.First;

		public const String UrlToSearch_key = "UrlToSearch";
		const String BundleKey_UrlToSearchFor = "UrlToSearch";
		const string BundleKey_RecentMode = "RecentMode";

		private FileDbHelper mDbHelper;
		private String mUrlToSearch;
		
		private bool recentMode = false;
		view.FileSelectButtons fileSelectButtons;
		bool createdWithActivityResult = false;

		IOConnectionInfo loadIoc(string defaultFileName)
		{
			return mDbHelper.cursorToIoc(mDbHelper.fetchFileByName(defaultFileName));
		}

		void ShowFilenameDialog(bool showOpenButton, bool showCreateButton, bool showBrowseButton, string defaultFilename, string detailsText, int requestCodeBrowse)
		{
			AlertDialog.Builder builder = new AlertDialog.Builder(this);
			builder.SetView(LayoutInflater.Inflate(Resource.Layout.file_selection_filename, null));
			Dialog dialog = builder.Create();
			dialog.Show();

			Button openButton = (Button)dialog.FindViewById(Resource.Id.open);
			Button createButton = (Button)dialog.FindViewById(Resource.Id.create);
			TextView enterFilenameDetails = (TextView)dialog.FindViewById(Resource.Id.label_open_by_filename_details);
			openButton.Visibility = showOpenButton ? ViewStates.Visible : ViewStates.Gone;
			createButton.Visibility = showCreateButton ? ViewStates.Visible : ViewStates.Gone;
			// Set the initial value of the filename
			EditText editFilename = (EditText)dialog.FindViewById(Resource.Id.file_filename);
			editFilename.Text = defaultFilename;
			enterFilenameDetails.Text = detailsText;
			enterFilenameDetails.Visibility = enterFilenameDetails.Text == "" ? ViewStates.Gone : ViewStates.Visible;

			// Open button
			
			openButton.Click += ( sender, evt) => {
				String fileName = ((EditText)dialog.FindViewById(Resource.Id.file_filename)).Text;
				
				IOConnectionInfo ioc = new IOConnectionInfo() { 
					Path = fileName
				};
				
				LaunchPasswordActivityForIoc(ioc);
			};
			
			
			
			// Create button
			createButton.Click += (sender, evt) => {
				String filename = Util.getEditText(this,
				                                   Resource.Id.file_filename);
				
				//TODO: allow non-local files?
				
				// Make sure file name exists
				if (filename.Length == 0)
				{
					Toast
						.MakeText(this,
						          Resource.String.error_filename_required,
						          ToastLength.Long).Show();
					return;
				}
				
				// Try to create the file
				Java.IO.File file = new Java.IO.File(filename);
				try
				{
					if (file.Exists())
					{
						Toast.MakeText(this,
						               Resource.String.error_database_exists,
						               ToastLength.Long).Show();
						return;
					}
					Java.IO.File parent = file.ParentFile;
					
					if (parent == null || (parent.Exists() && ! parent.IsDirectory))
					{
						Toast.MakeText(this,
						               Resource.String.error_invalid_path,
						               ToastLength.Long).Show();
						return;
					}
					
					if (! parent.Exists())
					{
						// Create parent dircetory
						if (! parent.Mkdirs())
						{
							Toast.MakeText(this,
							               Resource.String.error_could_not_create_parent,
							               ToastLength.Long).Show();
							return;
							
						}
					}
					
					file.CreateNewFile();
				} catch (Java.IO.IOException ex)
				{
					Toast.MakeText(
						this,
						GetText(Resource.String.error_file_not_create) + " "
						+ ex.LocalizedMessage,
						ToastLength.Long).Show();
					return;
				}
				
				// Prep an object to collect a password once the database has
				// been created
				CollectPassword password = new CollectPassword(
					new LaunchGroupActivity(IOConnectionInfo.FromPath(filename), this), this);
				
				// Create the new database
				CreateDB create = new CreateDB(this, IOConnectionInfo.FromPath(filename), password, true);
				ProgressTask createTask = new ProgressTask(
					this, create,
					Resource.String.progress_create);
				createTask.run();
				
				
			};
			
			Button cancelButton = (Button)dialog.FindViewById(Resource.Id.fnv_cancel);
			cancelButton.Click += (sender, e) => { 
				dialog.Dismiss();
			};
			
			ImageButton browseButton = (ImageButton)dialog.FindViewById(Resource.Id.browse_button);
			if (!showBrowseButton)
			{
				browseButton.Visibility = ViewStates.Invisible;
			}
			browseButton.Click += (sender, evt) => {
				string filename = ((EditText)dialog.FindViewById(Resource.Id.file_filename)).Text;
				
				Util.showBrowseDialog(filename, this, requestCodeBrowse);
				
			};

		}		

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			Android.Util.Log.Debug("DEBUG", "FileSelect.OnCreate");

			if (Intent.Action == Intent.ActionSend)
				mUrlToSearch = Intent.GetStringExtra(Intent.ExtraText);
			else
				mUrlToSearch = Intent.GetStringExtra(UrlToSearch_key);


			mDbHelper = App.fileDbHelper;
			if (mDbHelper.hasRecentFiles())
			{
				recentMode = true;

				SetContentView(Resource.Layout.file_selection);
				fileSelectButtons = new keepass2android.view.FileSelectButtons(this);
				((ListView)FindViewById(Android.Resource.Id.List)).AddFooterView(
					fileSelectButtons);

			} else
			{
				SetContentView(Resource.Layout.file_selection_no_recent);
				fileSelectButtons = (view.FileSelectButtons)FindViewById(Resource.Id.file_select);
			}



			Button openFileButton = (Button)FindViewById(Resource.Id.start_open_file);


			EventHandler openFileButtonClick = (object sender, EventArgs e) => 
			{
				string defaultFilename = Android.OS.Environment.ExternalStorageDirectory + GetString(Resource.String.default_file_path);
				string detailsText = "";
				ShowFilenameDialog(true, false, true, defaultFilename, detailsText, Intents.REQUEST_CODE_FILE_BROWSE_FOR_OPEN);

				                   
			};
			openFileButton.Click += openFileButtonClick;
			//OPEN URL
			Button openUrlButton = (Button)FindViewById(Resource.Id.start_open_url);

#if NoNet
			openUrlButton.Visibility = ViewStates.Gone;
#endif

			EventHandler openUrlButtonClick = (object sender, EventArgs e) => 
			{
				ShowFilenameDialog(true, false, false, "", GetString(Resource.String.enter_filename_details_url), Intents.REQUEST_CODE_FILE_BROWSE_FOR_OPEN);
			};
			openUrlButton.Click += openUrlButtonClick;

			//CREATE NEW
			Button createNewButton = (Button)FindViewById(Resource.Id.start_create);
			EventHandler createNewButtonClick = (object sender, EventArgs e) => 
			{
				ShowFilenameDialog(false, true, true, Android.OS.Environment.ExternalStorageDirectory + GetString(Resource.String.default_file_path), "", Intents.REQUEST_CODE_FILE_BROWSE_FOR_CREATE);
			};
			createNewButton.Click += createNewButtonClick;

			/*//CREATE + IMPORT
			Button createImportButton = (Button)FindViewById(Resource.Id.start_create_import);
			createImportButton.Click += (object sender, EventArgs e) => 
			{
				openButton.Visibility = ViewStates.Gone;
				createButton.Visibility = ViewStates.Visible;
				enterFilenameDetails.Text = GetString(Resource.String.enter_filename_details_create_import);
				enterFilenameDetails.Visibility = enterFilenameDetails.Text == "" ? ViewStates.Gone : ViewStates.Visible;
				// Set the initial value of the filename
				EditText filename = (EditText)FindViewById(Resource.Id.file_filename);
				filename.Text = Android.OS.Environment.ExternalStorageDirectory + GetString(Resource.String.default_file_path);

			};*/

			fillData();
			
			RegisterForContextMenu(ListView);

			if (savedInstanceState != null)
			{
				mUrlToSearch = savedInstanceState.GetString(BundleKey_UrlToSearchFor, null);
				recentMode = savedInstanceState.GetBoolean(BundleKey_RecentMode, recentMode);


			}

		}


		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			outState.PutString(BundleKey_UrlToSearchFor, mUrlToSearch);
			outState.PutBoolean(BundleKey_RecentMode, recentMode);
		}
		
		private class LaunchGroupActivity : FileOnFinish {

			FileSelectActivity activty;
			private IOConnectionInfo mIoc;
			
			public LaunchGroupActivity(IOConnectionInfo ioc, FileSelectActivity activty): base(null) {

				this.activty = activty;
				mIoc = ioc;
			}
			
			public override void run() {
				if (mSuccess) {
					// Add to recent files
					FileDbHelper dbHelper = App.fileDbHelper;

					//TODO: getFilename always returns "" -> bug?
					dbHelper.createFile(mIoc, getFilename());
					
					GroupActivity.Launch(activty);
					
				} else {
					IOConnection.DeleteFile(mIoc);
				}
			}
		}
		
		private class CollectPassword: FileOnFinish {
			FileSelectActivity activity;
			FileOnFinish mFileOnFinish;
			public CollectPassword(FileOnFinish finish,FileSelectActivity activity):base(finish) {
				this.activity = activity;
				mFileOnFinish = finish;
			}
			
			public override void run() {
				SetPasswordDialog password = new SetPasswordDialog(activity, mFileOnFinish);
				password.Show();
			}
			
		}
		
		private void fillData() {

			// Get all of the rows from the database and create the item list
			Android.Database.ICursor filesCursor = mDbHelper.fetchAllFiles();
			StartManagingCursor(filesCursor);
			
			// Create an array to specify the fields we want to display in the list
			// (only TITLE)
			String[] from = new String[] { FileDbHelper.KEY_FILE_FILENAME };
			
			// and an array of the fields we want to bind those fields to (in this
			// case just text1)
			int[] to = new int[] { Resource.Id.file_filename };
			
			// Now create a simple cursor adapter and set it to display
			SimpleCursorAdapter notes = new SimpleCursorAdapter(this,
			                                                    Resource.Layout.file_row, filesCursor, from, to);


			ListAdapter = notes;
		}


		void LaunchPasswordActivityForIoc(IOConnectionInfo ioc)
		{
			if ((!ioc.IsLocalFile()) && (ioc.CredSaveMode != IOCredSaveMode.SaveCred))
			{
				//Build dialog to query credentials:
				AlertDialog.Builder builder = new AlertDialog.Builder(this);
				builder.SetTitle(GetString(Resource.String.credentials_dialog_title));
				builder.SetPositiveButton(GetString(Android.Resource.String.Ok), new EventHandler<DialogClickEventArgs>((dlgSender, dlgEvt) => 
				{
					Dialog dlg = (Dialog)dlgSender;
					string username = ((EditText)dlg.FindViewById(Resource.Id.cred_username)).Text;
					string password = ((EditText)dlg.FindViewById(Resource.Id.cred_password)).Text;
					int credentialRememberMode = ((Spinner)dlg.FindViewById(Resource.Id.cred_remember_mode)).SelectedItemPosition;
					ioc.UserName = username;
					ioc.Password = password;
					ioc.CredSaveMode = (IOCredSaveMode)credentialRememberMode;
					PasswordActivity.Launch(this, ioc, mUrlToSearch);
				}));
				builder.SetView(LayoutInflater.Inflate(Resource.Layout.url_credentials, null));
				builder.SetNeutralButton(GetString(Android.Resource.String.Cancel), 
				                         new EventHandler<DialogClickEventArgs>((dlgSender, dlgEvt) => {}));
				Dialog dialog = builder.Create();
				dialog.Show();
				((EditText)dialog.FindViewById(Resource.Id.cred_username)).Text = ioc.UserName;
				((EditText)dialog.FindViewById(Resource.Id.cred_password)).Text = ioc.Password;
				((Spinner)dialog.FindViewById(Resource.Id.cred_remember_mode)).SetSelection((int)ioc.CredSaveMode);
			}
			else
			{
				try
				{
					PasswordActivity.Launch(this, ioc, mUrlToSearch);
				} catch (Java.IO.FileNotFoundException)
				{
					Toast.MakeText(this,     Resource.String.FileNotFound, ToastLength.Long).Show();
				} 
			}
		}
		
		protected override void OnListItemClick(ListView l, View v, int position, long id) {
			base.OnListItemClick(l, v, position, id);
			
			Android.Database.ICursor cursor = mDbHelper.fetchFile(id);
			StartManagingCursor(cursor);
			
			IOConnectionInfo ioc = mDbHelper.cursorToIoc(cursor);

			LaunchPasswordActivityForIoc(ioc);

		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);

			createdWithActivityResult = true;

			if (resultCode == KeePass.EXIT_CLOSE_AFTER_SEARCH)
			{
				Finish();
				return;
			}
			
			fillData();
			
			if ( (requestCode == Intents.REQUEST_CODE_FILE_BROWSE_FOR_CREATE
			      || requestCode == Intents.REQUEST_CODE_FILE_BROWSE_FOR_OPEN)
			    && resultCode == Result.Ok) {
				String filename = data.DataString;
				if (filename != null) {
					if (filename.StartsWith("file://")) {
						filename = filename.Substring(7);
					}
					
					filename = Java.Net.URLDecoder.Decode(filename);

					if (requestCode == Intents.REQUEST_CODE_FILE_BROWSE_FOR_OPEN)
					{
						IOConnectionInfo ioc = new IOConnectionInfo() { 
							Path = filename
						};
						
						LaunchPasswordActivityForIoc(ioc);
					}

					if (requestCode == Intents.REQUEST_CODE_FILE_BROWSE_FOR_CREATE)
					{
						ShowFilenameDialog(false, true, true, filename, "", Intents.REQUEST_CODE_FILE_BROWSE_FOR_CREATE);
					}
				}
				
			}
		}


		protected override void OnResume()
		{
			base.OnResume();
			Android.Util.Log.Debug("DEBUG", "FileSelect.OnResume");
			
			// Check to see if we need to change modes
			if (mDbHelper.hasRecentFiles() != recentMode)
			{
				// Restart the activity
				Intent intent = this.Intent;
				StartActivity(intent);
				Finish();
			}


			fileSelectButtons.updateExternalStorageWarning();

			if (!createdWithActivityResult)
			{
				if ((Intent.Action == Intent.ActionSend) && (App.getDB().Loaded))
				{
					PasswordActivity.Launch(this, App.getDB().mIoc , mUrlToSearch);
				} else
				{
					
					// Load default database
					ISharedPreferences prefs = Android.Preferences.PreferenceManager.GetDefaultSharedPreferences(this);
					String defaultFileName = prefs.GetString(PasswordActivity.KEY_DEFAULT_FILENAME, "");
					
					if (defaultFileName.Length > 0)
					{
						Java.IO.File db = new Java.IO.File(defaultFileName);
						
						if (db.Exists())
						{
							try
							{
								PasswordActivity.Launch(this, loadIoc(defaultFileName), mUrlToSearch);
							} catch (Exception e)
							{
								Toast.MakeText(this, e.Message, ToastLength.Long);
								// Ignore exception
							}
						}
					}
				}
			}

		}

		protected override void OnStart()
		{
			base.OnStart();
			Android.Util.Log.Debug("DEBUG", "FileSelect.OnStart");
		}
		public override bool OnCreateOptionsMenu(Android.Views.IMenu menu) {
			base.OnCreateOptionsMenu(menu);
			
			MenuInflater inflater = MenuInflater;
			inflater.Inflate(Resource.Menu.fileselect, menu);
			
			return true;
		}

		protected override void OnPause()
		{
			base.OnPause();
			Android.Util.Log.Debug("DEBUG", "FileSelect.OnPause");
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			GC.Collect();
			Android.Util.Log.Debug("DEBUG", "FileSelect.OnDestroy"+IsFinishing.ToString());
		}

		protected override void OnStop()
		{
			base.OnStop();
			Android.Util.Log.Debug("DEBUG", "FileSelect.OnStop");
		}

		public override bool OnOptionsItemSelected(Android.Views.IMenuItem item) {
			switch (item.ItemId) {
			case Resource.Id.menu_donate:
				try {
						Util.gotoDonateUrl(this);
				} catch (ActivityNotFoundException) {
					Toast.MakeText(this, Resource.String.error_failed_to_launch_link, ToastLength.Long).Show();
					return false;
				}
				
				return true;
			
			case Resource.Id.menu_about:
				AboutDialog dialog = new AboutDialog(this);
				dialog.Show();
				return true;
				
			case Resource.Id.menu_app_settings:
				AppSettingsActivity.Launch(this);
				return true;
			}
			
			return base.OnOptionsItemSelected(item);
		}
		
		public override void OnCreateContextMenu(Android.Views.IContextMenu menu, View v,
		                                         Android.Views.IContextMenuContextMenuInfo menuInfo) {
			base.OnCreateContextMenu(menu, v, menuInfo);
			
			menu.Add(0, CMENU_CLEAR, 0, Resource.String.remove_from_filelist);
		}
		
		public override bool OnContextItemSelected(Android.Views.IMenuItem item) {
			base.OnContextItemSelected(item);
			
			if ( item.ItemId == CMENU_CLEAR ) {
				AdapterView.AdapterContextMenuInfo acmi = (AdapterView.AdapterContextMenuInfo) item.MenuInfo;
				
				TextView tv = (TextView) acmi.TargetView;
				String filename = tv.Text;
				mDbHelper.deleteFile(filename);

				refreshList();
				
				
				return true;
			}
			
			return false;
		}
		
		private void refreshList() {
			CursorAdapter ca = (CursorAdapter) ListAdapter;
			Android.Database.ICursor cursor = ca.Cursor;
			cursor.Requery();
		}
	}
}
