using System;
using UnityEngine;

namespace TestLite
{
	public static class Logging
	{
		public static void Log(string text)
		{
			Debug.Log("[TestLite] " + text);
		}
		public static void LogFormat(string format, params object[] args)
		{
			Debug.LogFormat("[TestLite] " + format, args);
		}
		public static void LogWarningFormat(string format, params object[] args)
		{
			Debug.LogWarningFormat("[TestLite] " + format, args);
		}
		public static void LogException(Exception e)
		{
			Debug.LogException(e);
		}
	}
}
