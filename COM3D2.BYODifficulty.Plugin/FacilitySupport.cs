using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace COM3D2.BYODifficulty.Plugin
{

	class FacilitySupport
	{
		public static FacilityManager FacilityMan;

		[HarmonyPatch(typeof(FacilityManager), "Init")]
		[HarmonyPrefix]
		static void GetFacilityManager(ref FacilityManager __instance)
		{
			FacilityMan = __instance;
		}
	}
}
