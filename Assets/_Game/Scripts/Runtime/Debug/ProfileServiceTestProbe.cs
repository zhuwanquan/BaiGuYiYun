using UnityEngine;

namespace BaiguVN
{
    public class ProfileServiceTestProbe
        : MonoBehaviour
    {
        private const string TestId =
            "__P7_PROFILE_TEST__";

        [ContextMenu(
            "P7 Test - Write Profile")]
        private void WriteProfile()
        {
            ProfileService service =
                new ProfileService();

            VNProfile profile =
                service.LoadOrCreate();

            if (!profile.readNodeIds
                .Contains(TestId))
            {
                profile.readNodeIds.Add(
                    TestId
                );
            }

            service.SaveProfile(
                profile
            );

            Debug.Log(
                "P7 Profile 测试写入完成。"
            );
        }

        [ContextMenu(
            "P7 Test - Read Profile")]
        private void ReadProfile()
        {
            ProfileService service =
                new ProfileService();

            VNProfile profile =
                service.LoadOrCreate();

            bool found =
                profile.readNodeIds
                    .Contains(TestId);

            Debug.Log(
                $"P7 Profile 测试读取：" +
                $"{found}"
            );
        }

        [ContextMenu(
            "P7 Test - Cleanup Marker")]
        private void CleanupMarker()
        {
            ProfileService service =
                new ProfileService();

            VNProfile profile =
                service.LoadOrCreate();

            profile.readNodeIds.Remove(
                TestId
            );

            service.SaveProfile(
                profile
            );

            Debug.Log(
                "P7 Profile 测试标记已清理。"
            );
        }
    }
}