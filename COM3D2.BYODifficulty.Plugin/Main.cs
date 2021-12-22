using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Schedule;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace COM3D2.BYODifficulty.Plugin
{
	[BepInPlugin("BYODifficulty", "BYODifficulty", "1.0")]
	public class Main : BaseUnityPlugin
	{
		private Harmony harmony;
		public static Main instance;
		public static ManualLogSource logger;

		public static ConfigEntry<double> IncomeMultiplier { get; set; }
		public static ConfigEntry<long> FixedSubtraction { get; set; }
		public static ConfigEntry<long> BillOfFacilities { get; set; }
		public static ConfigEntry<long> MaidWorkWage { get; set; }
		public static ConfigEntry<double> MaidCutOfEarnings { get; set; }
		public static ConfigEntry<double> TraineeMaidMultiplier { get; set; }
		public static ConfigEntry<double> PersonalMaidMultiplier { get; set; }
		public static ConfigEntry<double> FreeMaidMultiplier { get; set; }
		public static ConfigEntry<double> NPCMaidMultiplier { get; set; }
		public static ConfigEntry<long> MaidBaseWage { get; set; }
		public static ConfigEntry<double> ClubGradeDebuff { get; set; }
		public static ConfigEntry<double> ClubGaugeDebuff { get; set; }
		public static ConfigEntry<int> ClubGaugePenalty { get; set; }
		public static ConfigEntry<double> ClubGaugeMultiplier { get; set; }
		public static ConfigEntry<long> BankruptcyThreshold { get; set; }

		void Awake()
		{
			logger = this.Logger;

			harmony = Harmony.CreateAndPatchAll(typeof(PlayerStatusSupport));

			harmony.PatchAll(typeof(FacilitySupport));

			IncomeMultiplier = this.Config.Bind("Income Modifiers", "Income Multiplier", 0.125, "Your income will be multiplied by this value. 0.5 is half your income is recieved. 1.0 is normal income recieved.");

			FixedSubtraction = this.Config.Bind("Income Modifiers", "Income Subtraction", 10000L, "Your income will be reduced by this amount at the end of every day. Think of it as bill payments... Or maybe what you pay your wife's bull if you're into that...");

			BillOfFacilities = this.Config.Bind("Income Modifiers", "Bill Per Facility", 3500L, "At the end of the day, your facilities are counted and multiplied by this amount. This value is then subtracted.");

			MaidWorkWage = this.Config.Bind("Income Modifiers", "Maid Work Wages", 3000L, "What you pay a single maid for working. This value is multiplied by the amount of maids that worked. They are paid per task immediately after each task.");

			MaidCutOfEarnings = this.Config.Bind("Income Modifiers", "Maids Cut Of Earnings", 0.15, "What percent of her earnings the made takes for herself. This is further multiplied by her wage multiplier. So if a trainee maid makes money, and her cut is 50%, she will only actually get 25%");

			MaidBaseWage = this.Config.Bind("Income Modifiers", "Maid Base Wages", 1000L, "What you pay a single maid while she's hired. She will be paid this amount at the end of the day regardless if she worked or not. This value is multiplied by maids hired.");

			TraineeMaidMultiplier = this.Config.Bind("Income Modifiers", "Trainee Maid Wage Multiplier", 0.50, "Maids of this contract type will have all their wages multiplied by this amount before disembursement.");
			PersonalMaidMultiplier = this.Config.Bind("Income Modifiers", "Personal Maid Wage Multiplier", 1.0, "Maids of this contract type will have all their wages multiplied by this amount before disembursement.");
			FreeMaidMultiplier = this.Config.Bind("Income Modifiers", "Free Maid Wage Multiplier", 1.5, "Maids of this contract type will have all their wages multiplied by this amount before disembursement.");
			NPCMaidMultiplier = this.Config.Bind("Income Modifiers", "NPC Maid Wage Multiplier", 0.75, "Maids of this contract type will have all their wages multiplied by this amount before disembursement.");

			ClubGradeDebuff = Config.Bind("Club Modifiers", "Club Grade Debuff", 0.10, "If your money is ever negative, your club suffers a serious debuff and your club grade is multiplied by this amount. However, it returns to what it should be once your club isn't in debt anymore.");

			ClubGaugeDebuff = Config.Bind("Club Modifiers", "Club Gauge Debuff", 0.10, "If your money is ever negative, your club suffers a serious debuff and your club's gauge is multiplied by this amount. However, it returns to what it should be once your club isn't in debt anymore.");

			ClubGaugePenalty = Config.Bind("Club Modifiers", "Club Gauge Penalty", 5, "If you can't pay for Income Subtractions, you will instead take a hit to your Club Guage of this amount. The max value of club gauge is 100!");

			ClubGaugeMultiplier = Config.Bind("Club Modifiers", "Club Gauge Multiplier", 0.25, "By how much to multiply increases. If your this value is increased by 2. That 2 increase will be multiplied by this value.");

			BankruptcyThreshold = Config.Bind("Consequences", "Bankruptcy Threshold", -500000L, "If you ever fall to or below this amount, then you go bankrupt and get an immediate game over but can sell a non-important or NPC maid to save yourself...");

			ClubGradeDebuff.SettingChanged += (s, e) => PlayerStatusSupport.GenerateClubGradeDictionary();

			PlayerStatusSupport.GenerateClubGradeDictionary();

			instance = this;
		}

		public static IEnumerator BankruptcyCoroutine(Maid maid = null)
		{
			while (GameMain.Instance.SysDlg.isActiveAndEnabled)
			{
				//UnityEngine.Debug.Log("\n\nWaiting turn at textbox!");
				yield return new WaitForSeconds(2.0f);
			}

			if (maid != null)
			{
				GameMain.Instance.SysDlg.Show("Bankruptcy!\n" + $"You're in crippling debt and can't go on! You can quit now or sell {maid.status.fullNameJpStyle}... Do you accept?",
				SystemDialog.TYPE.YES_NO,
				new SystemDialog.OnClick(() =>
				{
					GameMain.Instance.SysDlg.Close();
					SellMaid(maid);
				}),
				new SystemDialog.OnClick(() =>
				{
					GameMain.Instance.SysDlg.Close();
					GameMain.Instance.LoadScene("SceneToTitle");
					GameMain.Instance.MainCamera.FadeOut(0f, false, null, true, default(Color));
				}));
			}
			else 
			{
				GameMain.Instance.SysDlg.Show($"You've gone bankrupt and can sell no maids! GAME OVER!",
				SystemDialog.TYPE.OK,
				new SystemDialog.OnClick(() => 
				{
					GameMain.Instance.SysDlg.Close();
					GameMain.Instance.LoadScene("SceneToTitle");
					GameMain.Instance.MainCamera.FadeOut(0f, false, null, true, default(Color));
				}));
			}
			yield return null;
		}

		private static void SellMaid(Maid maid)
		{

			long saleAmount = Math.Abs(BankruptcyThreshold.Value) + new Random().Next(0, (int)(Math.Abs(BankruptcyThreshold.Value) * 0.25));

			GameMain.Instance.SysDlg.Show($"{maid.status.fullNameJpStyle} was sold to {MaidRandomName.GetPlayerName() + MaidRandomName.GetLastName()} for {saleAmount}",
			SystemDialog.TYPE.OK);

			PlayerStatusSupport.status.money_ += saleAmount;

			GameMain.Instance.CharacterMgr.BanishmentMaid(maid, false);
		}
	}
}