using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
public enum LowLevelBodyType
{
    StarClassA,
    StarClassB,
    StarClassF,
    StarClassG,
    StarClassK,
    StarClassM,
    StarClassO,
    ChthonianPlanet,
    CarbonPlanet,
    CorelessPlanet,
    DesertPlanet,
    GasDwarf,
    GasGiant,
    HeliumPlanet,
    IceGiant,
    IcePlanet,
    IronPlanet,
    LavaPlanet,
    OceanPlanet,
    ProtoPlanet,
    PuffyPlanet,
    SilicatePlanet,
    TerrestrialPlanet,
    Unknow,
    Determine
}
public enum HighLevelBodyType
{
    Star,
    Moon,
    Planet,
    Unknown,
    Determine
}
public enum RollType
{
    Normal,
    Advantaged,
    Disadvantaged
}
public class BodyContainer
{
    public GameObject BodyReference;
    public Material BodyMaterial;
    public HighLevelBodyType HLType;
    public LowLevelBodyType LLType;
}
public class SolarSpawner : MonoBehaviour
{

    [SerializeField] private GameObject[] BodyPrefabs;
    private Dictionary<LowLevelBodyType, BodyContainer> LLTypeToBodyContainer = new Dictionary<LowLevelBodyType, BodyContainer>();
    private Dictionary<HighLevelBodyType, List<BodyContainer>> HLTypeToBodyContainer = new Dictionary<HighLevelBodyType, List<BodyContainer>>();
    [SerializeField]
    private float MinPos = -10f;
    [SerializeField]
    private float MaxPos = +10f;
    [SerializeField]
    private float YPos = 0;
    // Start is called before the first frame update
    private void Start()
    {
        GetBodies();
        Debug.Log("Finish");
        List<BodyContainer> StarList = HLTypeToBodyContainer[HighLevelBodyType.Star];
        int Star = RollSingleDice(HLTypeToBodyContainer.Count);
        
        Vector3 position = new Vector3(UnityEngine.Random.Range(MinPos, MaxPos), YPos, UnityEngine.Random.Range(MinPos, MaxPos));
        BodyScript NewStar = Instantiate(StarList[Star].BodyReference, position, Quaternion.identity).GetComponent<BodyScript>();
        float Scale = UnityEngine.Random.Range(NewStar.ScaleMin, NewStar.ScaleMax)/10;
        NewStar.scale = new float3(Scale);
        BodyScript[] SolarChildren = new BodyScript[UnityEngine.Random.Range(4, 11)];
        
        for (int childIndex = 0; childIndex < SolarChildren.Length; childIndex++)
        {
            if(childIndex == 0)
            {
                SolarChildren[childIndex] = SpawnAndPositionPlanet(NewStar, Scale);
            }
            else
            {
                SolarChildren[childIndex] = SpawnAndPositionPlanet(SolarChildren[childIndex - 1], Scale);
            }
            SolarChildren[childIndex].transform.RotateAround(position, Vector3.up, UnityEngine.Random.Range(-180, 180));
        }
        

    }
    
    private BodyScript SpawnAndPositionPlanet(BodyScript previousBody, float StarSize)
    {
        float MinObitalDistence = previousBody.scale.x * 1.5f;
        float CurrentMinDistence = MinObitalDistence * 2;
        float CurrentMaxDistence = MinObitalDistence + (StarSize * 1f);
        Vector3 BodyPosition = previousBody.transform.position;
        Vector3 position = new Vector3(BodyPosition.x, BodyPosition.y, BodyPosition.z + UnityEngine.Random.Range(CurrentMinDistence, CurrentMaxDistence));
        List<BodyContainer> PlanetList = HLTypeToBodyContainer[HighLevelBodyType.Planet];
        BodyScript NewPlanet = Instantiate(PlanetList[RollSingleDice(HLTypeToBodyContainer.Count)].BodyReference, position, Quaternion.identity).GetComponent<BodyScript>();
        float Scale = UnityEngine.Random.Range(NewPlanet.ScaleMin, NewPlanet.ScaleMax) / 10;
        NewPlanet.scale = new float3(Scale);
        return NewPlanet;
    }


    public int RollSingleDice(int Sides = 20, RollType AdvDisAdvNorm = RollType.Normal)
    {
        int Result;
        if (AdvDisAdvNorm == RollType.Normal)
        {
            Result = UnityEngine.Random.Range(0, Sides);
        }
        else // Else it must be Advantaged or Disadvantaged.
        {

            int A = UnityEngine.Random.Range(0, Sides);
            int B = UnityEngine.Random.Range(0, Sides);

            if (AdvDisAdvNorm == RollType.Advantaged)
            {
                if (A >= B)
                    Result = A;
                else
                    Result = B;
            }
            else // Else it must be Disadvantaged as it cannot be anything else.
            {
                if (A <= B)
                    Result = A;
                else
                    Result = B;
            }
        }
        return Result;
    }
    void GetBodies()
    {
        BodyScript[] bodyScripts = new BodyScript[BodyPrefabs.Length];

        for (int BodyPrefabIndex = 0; BodyPrefabIndex < BodyPrefabs.Length; BodyPrefabIndex++)
        {
            bodyScripts[BodyPrefabIndex] = BodyPrefabs[BodyPrefabIndex].GetComponent<BodyScript>();
        }
        for (int bodyScriptIndex = 0; bodyScriptIndex < bodyScripts.Length; bodyScriptIndex++)
        {
            BodyScript bodyScript = bodyScripts[bodyScriptIndex];
            if (!LLTypeToBodyContainer.ContainsKey(bodyScript.LowLevelType))
            {
                BodyContainer container = new BodyContainer
                {
                    BodyReference = bodyScript.gameObject,
                    BodyMaterial = bodyScript.material,
                    HLType = bodyScript.HighLevelType,
                    LLType = bodyScript.LowLevelType
                };

                LLTypeToBodyContainer.Add(bodyScript.LowLevelType, container);

                if (HLTypeToBodyContainer.ContainsKey(container.HLType))
                {
                    HLTypeToBodyContainer[container.HLType].Add(container);
                }
                else
                {
                    HLTypeToBodyContainer.Add(container.HLType, new List<BodyContainer> { container });
                }
            }
        }
    }

    // Update is called once per frame
    private void Update()
    {
        
    }
}
