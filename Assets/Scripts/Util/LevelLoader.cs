using UnityEngine;

namespace Util
{
    public class LevelLoader : MonoBehaviour
    {
        public static GameObject parentForItems;
        public GameObject MenuLevelWorldPrefab,
            RaceLevelWorldPrefab,
            SurvivalLevelWorldPrefab,
            PointsLevelWorldPrefab,
            FinalLevelWorldPrefab;

        public AudioClip menuSong, raceSong, surviveSong;
        
        public AudioSource musicSource;

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
            
            
            AudioClip clipToPlay = null;
            switch (newLevel)
            {
                case GameManager.GameLevel.MenuLevel:
                    clipToPlay = menuSong;
                    break;
                // Race and Points levels share the same song
                case GameManager.GameLevel.RaceLevel:
                case GameManager.GameLevel.PointsLevel:
                    clipToPlay = raceSong;
                    break;
                // Survival and the Final level share the same song
                case GameManager.GameLevel.SurvivalLevel:
                case GameManager.GameLevel.FinalLevel:
                    clipToPlay = surviveSong;
                    break;
            }

            // Play the selected song if the AudioSource and AudioClip are assigned
            if (musicSource != null && clipToPlay != null)
            {
                // Assign the new clip to the source and play it
                musicSource.clip = clipToPlay;
                musicSource.Play();
            }
            
            
            
            
            
            
            
            

            worldLevel = Instantiate(newLevelToBuildPrefab, Vector3.zero, Quaternion.identity, new InstantiateParameters
            {
                parent = null,
                worldSpace = true
            });
            worldLevel.name = "WorldLevel";
            parentForItems = worldLevel;


        }
    }
}