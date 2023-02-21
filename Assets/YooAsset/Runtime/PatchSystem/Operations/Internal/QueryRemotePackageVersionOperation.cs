using AquaSys.Tools;
using System.IO;

namespace YooAsset
{
	internal class QueryRemotePackageVersionOperation : AsyncOperationBase
	{
		private enum ESteps
		{
			None,
			DownloadPackageVersion,
			Done,
		}

		private static int RequestCount = 0;
		private readonly IRemoteServices _remoteServices;
		private readonly string _packageName;
		private readonly string _packageVersion;
		private readonly bool _appendTimeTicks;
		private readonly int _timeout;
		private UnityWebDataRequester _downloader;
		private ESteps _steps = ESteps.None;

		/// <summary>
		/// 包裹版本
		/// </summary>
		public YooAssetVersion PackageVersion { private set; get; }
		

		public QueryRemotePackageVersionOperation(IRemoteServices remoteServices, string packageName, string packageVersion, bool appendTimeTicks, int timeout)
		{
			_remoteServices = remoteServices;
			_packageName = packageName;
			_packageVersion = packageVersion;
			_appendTimeTicks = appendTimeTicks;
			_timeout = timeout;
		}
		internal override void Start()
		{
			RequestCount++;
			_steps = ESteps.DownloadPackageVersion;
		}
		internal override void Update()
		{
			if (_steps == ESteps.None || _steps == ESteps.Done)
				return;

			if (_steps == ESteps.DownloadPackageVersion)
			{
				if (_downloader == null)
				{
					string fileName = YooAssetSettingsData.GetPackageVersionFileName(_packageName, _packageVersion);
					string webURL = GetPackageVersionRequestURL(fileName, _packageName);
					YooLogger.Log($"Beginning to request package version : {webURL}");
					_downloader = new UnityWebDataRequester();
					_downloader.SendRequest(webURL, _timeout);
				}

				Progress = _downloader.Progress();
				if (_downloader.IsDone() == false)
					return;

				if (_downloader.HasError())
				{
					_steps = ESteps.Done;
					Status = EOperationStatus.Failed;
					Error = _downloader.GetError();
				}
				else
				{
					PackageVersion = StreamTools.DeserializeObject<YooAssetVersion> (_downloader.GetText());
					if (PackageVersion==null)
					{
						_steps = ESteps.Done;
						Status = EOperationStatus.Failed;
						Error = $"Remote package version is empty : {_downloader.URL}";
					}
					else
					{
						_steps = ESteps.Done;
						Status = EOperationStatus.Succeed;
					}
				}

				_downloader.Dispose();
			}
		}

		private string GetPackageVersionRequestURL(string fileName, string packageName)
		{
			string url;

			// 轮流返回请求地址
			if (RequestCount % 2 == 0)
				url = _remoteServices.GetRemoteFallbackURL(fileName, packageName);
			else
				url = _remoteServices.GetRemoteMainURL(fileName, packageName);

			// 在URL末尾添加时间戳
			if (_appendTimeTicks)
				return $"{url}?{System.DateTime.UtcNow.Ticks}";
			else
				return url;
		}
	}
}