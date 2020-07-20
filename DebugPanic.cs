using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// デバッグ用パニック検知
/// </summary>
[InitializeOnLoad]
public static class DebugPanic
{
	#region property

	/// <summary>
	/// 状態
	/// </summary>
	private enum State
	{
		/// <summary>
		/// 何もしない
		/// </summary>
		None,
		/// <summary>
		/// 監視中
		/// </summary>
		Detection,
		/// <summary>
		/// 検知した
		/// </summary>
		Error,
	}

	/// <summary>
	/// 検知レベル
	/// </summary>
	private enum DetectionLevel
	{
		/// <summary>
		/// ガバ
		/// </summary>
		None,
		/// <summary>
		/// Exception
		/// </summary>
		Soft,
		/// <summary>
		/// Exception,Assert
		/// </summary>
		Medium,
		/// <summary>
		/// Exception,Assert,Error
		/// </summary>
		Hard,
		/// <summary>
		/// Exception,Assert,Error,Warning
		/// </summary>
		VeryHard,
	}

	private static readonly string[] MenuItemPath = new string[]
	{
		"Tools/DebugPanicLevel/None",
		"Tools/DebugPanicLevel/Soft",
		"Tools/DebugPanicLevel/Medium",
		"Tools/DebugPanicLevel/Hard",
		"Tools/DebugPanicLevel/VeryHard"
	};

	private static State _state = State.None;

	private static DetectionLevel _level = DetectionLevel.Hard;

	private static GameObject _panicObject;

	private static RawImage _backGround;

	private static Text _text;

	#endregion

	static DebugPanic()
	{
		if(EditorApplication.isPlayingOrWillChangePlaymode)
		{
			_level = GetDetectionLevelByMenuChecked();

			SetState(State.Detection);
		}

		EditorApplication.playModeStateChanged += OnChangedPlayMode;

		UpdateMenuItemChecked();
	}

	#region menu

	[MenuItem("Tools/DebugPanicLevel/None", priority = 101)]
	private static void DetectionLevelNone()
	{
		_level = DetectionLevel.None;
		UpdateMenuItemChecked();
	}

	[MenuItem("Tools/DebugPanicLevel/Soft", priority = 102)]
	private static void DetectionLevelSoft()
	{
		_level = DetectionLevel.Soft;
		UpdateMenuItemChecked();
	}

	[MenuItem("Tools/DebugPanicLevel/Medium", priority = 103)]
	private static void DetectionLevelMedium()
	{
		_level = DetectionLevel.Medium;
		UpdateMenuItemChecked();
	}

	[MenuItem("Tools/DebugPanicLevel/Hard", priority = 104)]
	private static void DetectionLevelHard()
	{
		_level = DetectionLevel.Hard;
		UpdateMenuItemChecked();
	}

	[MenuItem("Tools/DebugPanicLevel/VeryHard", priority = 105)]
	private static void DetectionLevelVeryHard()
	{
		_level = DetectionLevel.VeryHard;
		UpdateMenuItemChecked();
	}

	[MenuItem("Tools/DebugPanicLevel/ShowInExplorer", priority = 255)]
	private static void ShowInExplorer()
	{
		// 一つ上の階層が開かれてしまうのでProcessに変更
		// EditorUtility.RevealInFinder(Application.persistentDataPath);

		Process.Start(Application.persistentDataPath);
	}

	private static void UpdateMenuItemChecked()
	{
		for(int i = 0; i < MenuItemPath.Length; ++i)
		{
			Menu.SetChecked(MenuItemPath[i], (_level == (DetectionLevel)i));
		}
	}

	private static DetectionLevel GetDetectionLevelByMenuChecked()
	{
		DetectionLevel level = DetectionLevel.None;

		for(int i = 0; i < MenuItemPath.Length; ++i)
		{
			if(Menu.GetChecked(MenuItemPath[i]))
			{
				level = (DetectionLevel)i;
				break;
			}
		}

		return level;
	}

	#endregion

	#region event

	private static void OnChangedPlayMode(PlayModeStateChange state)
	{
		switch(state)
		{
		// 再生時にコンストラクタが走るので初期化に使えない
		// case PlayModeStateChange.ExitingEditMode:
		// オブジェクトにアタッチされたスクリプトのほうが実行が早い場合がある
		// case PlayModeStateChange.EnteredPlayMode:
			// SetState(State.Detection);
			// break;
		case PlayModeStateChange.ExitingPlayMode:
			SetState(State.None);
			break;
		}
	}

	/// <summary>
	/// ログのハンドリング
	/// </summary>
	/// <param name="condition"></param>
	/// <param name="stackTrace"></param>
	/// <param name="type"></param>
	private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
	{
		if(_state != State.Detection) return;

		if(IsPanic(type) == false) return;

		SetState(State.Error);

		CreatePanicObject(condition, stackTrace);

		Screenshot();
	}

	#endregion

	#region method

	/// <summary>
	/// 状態を設定する
	/// </summary>
	/// <param name="state"></param>
	private static void SetState(State state)
	{
		_state = state;

		switch(_state)
		{
		case State.None:
			Application.logMessageReceived -= OnLogMessageReceived;
			Destroy();
			break;
		case State.Detection:
			Application.logMessageReceived += OnLogMessageReceived;
			break;
		case State.Error:
			Application.logMessageReceived -= OnLogMessageReceived;
			break;
		}
	}

	/// <summary>
	/// 検知
	/// </summary>
	/// <param name="type"></param>
	/// <returns></returns>
	private static bool IsPanic(LogType type)
	{
		bool ret = false;

		switch(_level)
		{
		case DetectionLevel.None:
			break;
		case DetectionLevel.Soft:
			ret = (type == LogType.Exception);
			break;
		case DetectionLevel.Medium:
			ret = (type == LogType.Exception);
			ret |= (type == LogType.Assert);
			break;
		case DetectionLevel.Hard:
			ret = (type == LogType.Exception);
			ret |= (type == LogType.Assert);
			ret |= (type == LogType.Error);
			break;
		case DetectionLevel.VeryHard:
			ret = (type == LogType.Exception);
			ret |= (type == LogType.Assert);
			ret |= (type == LogType.Error);
			ret |= (type == LogType.Warning);
			break;
		}

		return ret;
	}

	/// <summary>
	/// パニックオブジェクト作成
	/// </summary>
	/// <param name="condition"></param>
	/// <param name="stackTrace"></param>
	private static void CreatePanicObject(string condition, string stackTrace)
	{
		_panicObject = new GameObject("panic");
		Canvas canvas = _panicObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;

		GameObject backGroundObject = new GameObject("backGround");
		_backGround = backGroundObject.AddComponent<RawImage>();
		_backGround.rectTransform.SetParent(_panicObject.transform, false);
		_backGround.rectTransform.anchorMin = new Vector2(0, 0);
		_backGround.rectTransform.anchorMax = new Vector2(1, 1);
		_backGround.rectTransform.anchoredPosition = Vector2.zero;
		_backGround.rectTransform.sizeDelta = new Vector2(0, 0);
		_backGround.color = new Color(0, 0, 1, 0.5f);

		GameObject textObject = new GameObject("stackTrace");
		_text = textObject.AddComponent<Text>();
		_text.rectTransform.SetParent(_panicObject.transform, false);
		_text.rectTransform.anchorMin = new Vector2(0, 0);
		_text.rectTransform.anchorMax = new Vector2(1, 1);
		_text.rectTransform.anchoredPosition = Vector2.zero;
		_text.rectTransform.sizeDelta = new Vector2(0, 0);
		_text.horizontalOverflow = HorizontalWrapMode.Wrap;
		_text.verticalOverflow = VerticalWrapMode.Truncate;
		_text.alignment = TextAnchor.UpperLeft;
		_text.raycastTarget = false;
		_text.fontSize = 22;
		_text.color = Color.white;
		_text.font = Resources.GetBuiltinResource(typeof(Font), "Arial.ttf") as Font;

		_text.text = condition + "\n" + stackTrace;
	}

	/// <summary>
	/// スクショ
	/// </summary>
	private static void Screenshot()
	{
		// TODO:パス指定可能に
		string path = Application.persistentDataPath + Path.DirectorySeparatorChar;
		string filename = "Screenshot_" + System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".png";

		ScreenCapture.CaptureScreenshot(path + filename);
	}

	/// <summary>
	/// 破棄
	/// </summary>
	private static void Destroy()
	{
		if(_panicObject != null)
		{
			GameObject.DestroyImmediate(_panicObject);

			_panicObject = null;
			_backGround = null;
			_text = null;
		}
	}

	#endregion
}
