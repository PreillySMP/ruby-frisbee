using System;
using UnityEditor;
using UnityEngine;

namespace RubysFrisbee.Assets.Editor
{
    /// <summary>Optional CI entry point; never called automatically in an interactive Editor.</summary>
    public static class RubyPackageBatch
    {
        public static void BuildAndValidate()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use Tools/Ruby's Frisbee/Setup or Repair in an interactive Editor.");
            try
            {
                RubyPackageSetup.BuildSynchronouslyForBatch();
                AssetDatabase.SaveAssets();
                Debug.Log("Ruby package batch setup and native-asset validation succeeded.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
    }
}
