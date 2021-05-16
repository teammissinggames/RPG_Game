using UnityEngine;
using RPG_UI;
using RPG_Character;
using RPG_GameData;
using Cinemachine;

public class ExploreState : MonoBehaviour, IGameState
{
    public bool followCamera = true;
    public Character followCharacter;
    public Character Hero;
    public StateStack stack;
    public Map Map;

    public void Init(Map map, StateStack stack, Vector2 startPosition)
    {
        this.Map = map;
        this.stack = stack;
        LoadHero();
        // TODO remove test code
        var obj = ServiceManager.Get<AssetManager>().Load<Character>(Constants.TEST_NPC_PREFAB);
        if (obj != null)
        {
            var npc = Instantiate(obj);
            npc.transform.position = new Vector2(-4.0f, 0.0f);
            npc.transform.rotation = Quaternion.identity;
            npc.gameObject.transform.SetParent(transform, true);
            npc.Init(map, Constants.ENEMY_STATES, Constants.WAIT_STATE);
            var actor = npc.GetComponent<Actor>();
            actor.Init(ServiceManager.Get<GameData>().Enemies["goblin"]);
            ServiceManager.Get<NPCManager>().AddNPC(npc);
        }
    }

    public void HideHero()
    {
        Hero.Entity.Hide();
    }

    public void ShowHero()
    {
        Hero.Entity.Show();
    }

    public void UpdateCamera(Map map)
    {
        
    }

    public void Enter(object o) { }

    public void Exit() { }

    public bool Execute(float deltaTime)
    {
        UpdateCamera(Map);
        ServiceManager.Get<NPCManager>().UpdateAllNPCs(deltaTime);
        return true;
    }

    public void HandleInput()
    {
        if (ServiceManager.Get<World>().IsInputLocked())
        {
            LogManager.LogDebug("Input locked, not handling ExploreState input.");
            return;
        }
        Hero.Controller.Update(Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            var facingTile = Hero.GetFacingTilePosition();
            var x = (int)facingTile.x;
            var y = (int)facingTile.y;
            //if (Map.GetTrigger(x, y) is var trigger && trigger != null)
            //    trigger.OnUse(new TriggerParams(x,y,Hero));
            var targetPosition = new Vector2(x,y);
            var position = (Vector2)transform.position + targetPosition;
            //var collision = Physics2D.OverlapCircle(position, 0.2f, Hero.collisionLayer);
            var trigger = Map.GetTrigger((int)facingTile.x, (int)facingTile.y);
            //var trigger = collision.GetComponent<Trigger>();// != null
            //if (collision?.GetComponent<Trigger>() is var trigger && trigger != null)
            if (trigger != null)
            {
                trigger.OnUse(new TriggerParams(x,y,Hero.GetComponent<Character>()));
            }
        }
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            ServiceManager.Get<UIController>().OpenMenu(Map, stack);
        }
    }

    public string GetName()
    {
        return "ExploreState";
    }

    private void LoadHero()
    {
        var game = ServiceManager.Get<GameLogic>().GameState;
        if (game == null || game.World.Party.Count() < 1)
            LoadHeroPrefab();
        else
            Hero = game.World.Party.GetActor(0).GetComponent<Character>();
        Hero.transform.position = Vector2.zero;
        Hero.transform.rotation = Quaternion.identity;
    }

    private void LoadHeroPrefab()
    {
        var obj = ServiceManager.Get<AssetManager>().Load<Character>(Constants.HERO_PREFAB);
        if (obj == null)
        {
            LogManager.LogError("Unable to load hero in ExplroeState.");
            return;
        }
        Hero = Instantiate(obj);
        followCharacter = Hero;
        var world = ServiceManager.Get<World>();
        var characterParent = world.PersistentCharacters;
        Hero.gameObject.transform.SetParent(characterParent, true);
        Hero.Init(Map, Constants.PARTY_STATES, Constants.WAIT_STATE);
        var actor = Hero.GetComponent<Actor>();
        actor.Init(ServiceManager.Get<GameData>().PartyDefs["hero"]);
        world.Party.Add(actor);
        GameObject.Find("CM vcam1").GetComponent<CinemachineVirtualCamera>().Follow = Hero.transform;
    }
}