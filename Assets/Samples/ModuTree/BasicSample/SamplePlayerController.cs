using UnityEngine;

namespace ModuTree.Samples
{
    /// <summary>
    /// サンプルプレイヤーのコントローラー。
    /// WASD / 矢印キーで自由に移動できる。
    /// </summary>
    public class SamplePlayerController : MonoBehaviour
    {
        [Header("設定")]
        public float moveSpeed = 5f;

        private void Update()
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            var dir = new Vector3(h, 0f, v);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            if (dir.magnitude > 0.01f)
            {
                transform.position += dir * moveSpeed * Time.deltaTime;

                // 移動方向に向く
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    Time.deltaTime * 15f);
            }
        }
    }
}