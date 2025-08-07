using UnityEngine;

namespace Util
{
    public class LevelLoader : MonoBehaviour
    {
        public GameObject MenuLevelWorldPrefab,
            RaceLevelWorldPrefab,
            SurvivalLevelWorldPrefab,
            PointsLevelWorldPrefab,
            FinalLevelWorldPrefab;

        public void LoadLevel(GameManager.GameLevel newLevel)
        {
            var worldLevel = GameObject.Find("WorldLevel");
            Destroy(worldLevel);

            GameObject newLevelToBuildPrefab = null;
            switch (newLevel)
            {
                case GameManager.GameLevel.MenuLevel:
                    newLevelToBuildPrefab = MenuLevelWorldPrefab;
                    break;
                case GameManager.GameLevel.RaceLevel:
                    newLevelToBuildPrefab = RaceLevelWorldPrefab;
                    break;
                case GameManager.GameLevel.SurvivalLevel:
                    newLevelToBuildPrefab = SurvivalLevelWorldPrefab;
                    break;
                case GameManager.GameLevel.PointsLevel:
                    newLevelToBuildPrefab = PointsLevelWorldPrefab;
                    break;
                case GameManager.GameLevel.FinalLevel:
                    newLevelToBuildPrefab = FinalLevelWorldPrefab;
                    break;
            }

            worldLevel = Instantiate(newLevelToBuildPrefab, Vector3.zero, Quaternion.identity, new InstantiateParameters
            {
                parent = null,
                worldSpace = true
            });
            worldLevel.name = "WorldLevel";
        }
    }
}