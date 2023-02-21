
namespace YooAsset
{
	internal interface IRemoteServices
	{
		string GetRemoteMainURL(string fileName, string packageName);
		string GetRemoteFallbackURL(string fileName, string packageName);
	}
}