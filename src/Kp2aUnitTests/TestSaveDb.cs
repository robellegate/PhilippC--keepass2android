﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Android.App;
using Android.OS;
using KeePassLib;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using keepass2android;

namespace Kp2aUnitTests
{
	[TestClass]
	class TestSaveDb: TestBase
	{
		private string newFilename;

		[TestMethod]
		public void TestLoadEditSave()
		{
			//create the default database:
			IKp2aApp app = SetupAppWithDefaultDatabase();
			//save it and reload it so we have a base version
			SaveDatabase(app);
			app = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			//modify the database by adding a group:
			app.GetDb().KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "TestGroup", PwIcon.Apple), true);
			//save the database again:
			SaveDatabase(app);

			//load database to a new app instance:
			IKp2aApp resultApp = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//ensure the change was saved:
			AssertDatabasesAreEqual(app.GetDb().KpDatabase, resultApp.GetDb().KpDatabase);
		}

		[TestMethod]
		public void TestLoadEditSaveWithSync()
		{
			//create the default database:
			IKp2aApp app = SetupAppWithDefaultDatabase();
			//save it and reload it so we have a base version
			SaveDatabase(app);
			app = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			//load it once again:
			IKp2aApp app2 = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//modify the database by adding a group in both databases:
			app.GetDb().KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "TestGroup", PwIcon.Apple), true);
			var group2 = new PwGroup(true, true, "TestGroup2", PwIcon.Energy);
			app2.GetDb().KpDatabase.RootGroup.AddGroup(group2, true);
			//save the database from app 1:
			SaveDatabase(app);

			//save the database from app 2: This save operation must detect the changes made from app 1 and ask if it should sync:
			SaveDatabase(app2);

			//add group 2 to app 1:
			app.GetDb().KpDatabase.RootGroup.AddGroup(group2, true);

			//load database to a new app instance:
			IKp2aApp resultApp = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//ensure the sync was successful:
			AssertDatabasesAreEqual(app.GetDb().KpDatabase, resultApp.GetDb().KpDatabase);

			Assert.IsTrue(false, "todo: test for sync question, test overwrite or cancel!");
		}

		[TestMethod]
		public void TestLoadAndSave_TestIdenticalFiles()
		{
			IKp2aApp app = LoadDatabase(DefaultDirectory + "complexDb.kdbx", "test", null);
			var kdbxXml = DatabaseToXml(app);
			
			newFilename = TestDbDirectory + "tmp_complexDb.kdbx";
			if (File.Exists(newFilename))
				File.Delete(newFilename);
			app.GetDb().KpDatabase.IOConnectionInfo.Path = newFilename;
			app.GetDb().SaveData(Application.Context);


			IKp2aApp appReloaded = LoadDatabase(newFilename, "test", null);
			
			var kdbxReloadedXml = DatabaseToXml(appReloaded);

			Assert.AreEqual(kdbxXml,kdbxReloadedXml);
			


		}

		private class OnCloseToStringMemoryStream : MemoryStream
		{
			public string Text { get; private set; }
			private bool _closed;
			public override void Close()
			{
				if (!_closed)
				{
					Position = 0;
					Text = new StreamReader(this).ReadToEnd();	
				}
				base.Close();
				_closed = true;

			}
		}

		private static string DatabaseToXml(IKp2aApp app)
		{
			KdbxFile kdb = new KdbxFile(app.GetDb().KpDatabase);
			var sOutput = new OnCloseToStringMemoryStream();
			kdb.Save(sOutput, app.GetDb().KpDatabase.RootGroup, KdbxFormat.PlainXml, null);
			return sOutput.Text;
		}
	}
}