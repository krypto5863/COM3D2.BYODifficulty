using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace COM3D2.BYODifficulty.Plugin
{
	class PlayerStatusSupport
	{
		public static PlayerStatus.Status status;
		private static Dictionary<int, int> ClubGradeDebuffResults = new Dictionary<int, int>();

		public static void GenerateClubGradeDictionary()
		{
			for (int i = 1; i <= 5; i++)
			{
				ClubGradeDebuffResults[i] = Math.Max(1, (int)Math.Round(i * Main.ClubGradeDebuff.Value));
			}
		}

		[HarmonyPatch(typeof(PlayerStatus.Status), MethodType.Constructor)]
		[HarmonyPrefix]
		static void GetStatusInstance(ref PlayerStatus.Status __instance)
		{
			status = __instance;
		}

		[HarmonyPatch(typeof(ResultWorkMgr), "SetDescResultWorkDic")]
		[HarmonyPrefix]
		static void GetDictionary(ref Dictionary<string, ResultWorkCtrl.ResultWork> __0)
		{
			Dictionary<string, ResultWorkCtrl.ResultWork> MaidJobsByGUID = __0.Select(k => k.Value).Where(kv => kv.upperMaidStatus.maidId != null).ToDictionary(um => um.upperMaidStatus.maidId, um1 => um1);

			GameMain.Instance.CharacterMgr.GetStockMaidList().ForEach(maid => {

				if (MaidJobsByGUID.ContainsKey(maid.status.guid))
				{

					int cut = 0;
					int wage = 0;
					string type = "None";

					if (maid.status.heroineType == MaidStatus.HeroineType.Sub)
					{
						cut = (int)Math.Round(((MaidJobsByGUID[maid.status.guid].upperMaidStatus.income * Main.IncomeMultiplier.Value) * Main.MaidCutOfEarnings.Value) * Main.NPCMaidMultiplier.Value);

						wage = (int)Math.Round(Main.MaidWorkWage.Value * Main.NPCMaidMultiplier.Value);

						type = "NPC";
					}
					else if (maid.status.contract == MaidStatus.Contract.Trainee)
					{
						cut = (int)Math.Round(((MaidJobsByGUID[maid.status.guid].upperMaidStatus.income * Main.IncomeMultiplier.Value) * Main.MaidCutOfEarnings.Value) * Main.TraineeMaidMultiplier.Value);

						wage = (int)Math.Round(Main.MaidWorkWage.Value * Main.TraineeMaidMultiplier.Value);

						type = "Trainee";
					}
					else if (maid.status.contract == MaidStatus.Contract.Exclusive)
					{
						cut = (int)Math.Round(((MaidJobsByGUID[maid.status.guid].upperMaidStatus.income * Main.IncomeMultiplier.Value) * Main.MaidCutOfEarnings.Value) * Main.PersonalMaidMultiplier.Value);

						wage = (int)Math.Round(Main.MaidWorkWage.Value * Main.PersonalMaidMultiplier.Value);

						type = "Personal";
					}
					else if (maid.status.contract == MaidStatus.Contract.Free)
					{
						cut = (int)Math.Round(((MaidJobsByGUID[maid.status.guid].upperMaidStatus.income * Main.IncomeMultiplier.Value) * Main.MaidCutOfEarnings.Value) * Main.FreeMaidMultiplier.Value);

						wage = (int)Math.Round(Main.MaidWorkWage.Value * Main.FreeMaidMultiplier.Value);

						type = "Free";
					}

					status.money_ -= cut + wage;

					Main.logger.LogInfo($"Paid a {type} maid {maid.status.firstName} a cut of {cut} and a wage of {wage} with heroine type of {maid.status.heroineType.ToString()}. Main Character: {maid.status.mainChara}");
				}
			});
		}

		[HarmonyPatch(typeof(ResultIncomeMgr), "OpenResultIncomePanel")]
		[HarmonyPrefix]
		static void LoadConditions()
		{
			Main.logger.LogInfo($"Paid a base wage for {GameMain.Instance.CharacterMgr.GetStockMaidCount()} maids.");

			GameMain
			.Instance
			.CharacterMgr
			.GetStockMaidList()
			.ForEach(maid => {

				if (maid.status.heroineType == MaidStatus.HeroineType.Sub)
				{
					status.money -= (int)Math.Round(Main.MaidBaseWage.Value * Main.NPCMaidMultiplier.Value);
				}
				else if (maid.status.contract == MaidStatus.Contract.Trainee)
				{
					status.money -= (int)Math.Round(Main.MaidBaseWage.Value * Main.TraineeMaidMultiplier.Value);
				}
				else if (maid.status.contract == MaidStatus.Contract.Exclusive)
				{
					status.money -= (int)Math.Round(Main.MaidBaseWage.Value * Main.PersonalMaidMultiplier.Value);
				}
				else if (maid.status.contract == MaidStatus.Contract.Free)
				{
					status.money -= (int)Math.Round(Main.MaidBaseWage.Value * Main.FreeMaidMultiplier.Value);
				}
			});

			{
				long facilityBill = FacilitySupport
				.FacilityMan
				.GetFacilityArray()
				.Where(f => f != null)
				.Count() * Main.BillOfFacilities.Value;

				Main.logger.LogInfo($"Paid a total of {Main.FixedSubtraction.Value} for other expenses... And paid {facilityBill} for facilities...");

				status.money_ -= Main.FixedSubtraction.Value + facilityBill;
			}

			if (status.money_ < 0) 
			{
				Main.logger.LogInfo($"You are currently in debt! Your club can be affected by this!");

				status.clubGauge_ -= Main.ClubGaugePenalty.Value;
			}

			if (status.money_ <= Main.BankruptcyThreshold.Value)
			{
				var MaidList = GameMain.Instance.CharacterMgr
				.GetStockMaidList()
				.Where(m => m.status.heroineType != MaidStatus.HeroineType.Sub && m.status.mainChara == false)
				.ToArray();

				if (MaidList.Count() > 1) 
				{
					var MaidtoSell = MaidList[new Random().Next(0, MaidList.Count() - 1)] ?? null;
					Main.instance.StartCoroutine(Main.BankruptcyCoroutine(MaidtoSell));
				} else 
				{
					Main.instance.StartCoroutine(Main.BankruptcyCoroutine());
				}
			}
		}

		[HarmonyPatch(typeof(PlayerStatus.Status), "money", MethodType.Setter)]
		[HarmonyPrefix]
		static bool AllowNegatives(ref long __0)
		{
			//Makes no changes to subtractions of money. Players are free to lose all the money they want.
			if (status.money_ > __0)
			{
				status.money_ = __0;
				return false;
			}//No point in setting the same value again. Discard.
			else if (status.money_ == __0)
			{
				return false;
			}

			long moneyEarned = Math.Abs(__0 - status.money_);

			if (moneyEarned == 0)
			{
				return false;
			}

			Main.logger.LogInfo($"You earned {moneyEarned}");

			long adjustedPay = (long)(moneyEarned * Main.IncomeMultiplier.Value);

			Main.logger.LogInfo($"Your income was multipled by {Main.IncomeMultiplier.Value} for a total of {adjustedPay}. Setting money now...");

			status.money_ = adjustedPay + status.money_;

			return false;
		}

		[HarmonyPatch(typeof(PlayerStatus.Status), "clubGrade", MethodType.Getter)]
		[HarmonyPrefix]
		static bool ModifyClubGradeReturn(ref int __result)
		{
			if (status.money_ < Main.FixedSubtraction.Value)
			{
				__result = ClubGradeDebuffResults[status.clubGrade_];
				return false;
			}

			return true;
		}

		[HarmonyPatch(typeof(PlayerStatus.Status), "clubGauge", MethodType.Getter)]
		[HarmonyPrefix]
		static bool ModifyClubGaugeReturn(ref int __result)
		{
			if (status.money_ < Main.FixedSubtraction.Value)
			{
				__result = (int)Math.Round((status.clubGauge_ * Main.ClubGaugeDebuff.Value));

				Main.logger.LogInfo($"You're broke! Returning a club gauge of: {__result}");

				return false;
			}

			return true;
		}

		[HarmonyPatch(typeof(PlayerStatus.Status), "clubGauge", MethodType.Setter)]
		[HarmonyPrefix]
		static void ModifyClubGauge(ref int __0)
		{
			if (__0 <= status.clubGauge_)
			{
				return;
			}

			int diff = __0 - status.clubGauge_;

			Main.logger.LogInfo($"Club gauge is being increased by {diff} but this is adjusted to an increase of {Math.Round(diff * Main.ClubGaugeMultiplier.Value)}");

			__0 = (int)Math.Round((diff * Main.ClubGaugeMultiplier.Value) + status.clubGauge_);
		}
	}
}
