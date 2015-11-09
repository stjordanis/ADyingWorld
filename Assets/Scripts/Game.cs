﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum Direction
{
	NONE,
	UP,
	RIGHT,
	DOWN,
	LEFT
}

/// <summary>
/// The class for running the game.
/// </summary>
public class Game : MonoBehaviour
{
	// Constants
	public const int TURNS_PER_DAY = 360;
	public const float MOVING_TRANSITION_TIME = 0.2f;
	public const float ANIMATE_TILE_TIME = 0.25f;

	// Tile Sprite Sheets
	public Sprite[] tileGround;
	public Sprite[] tileOverGround;
	public Sprite[] tileWall;
	public Sprite[] tileDecorationBase;
	public Sprite[] tileWallTableDecoration;
	public Sprite[] tileRoof;
	public Sprite[] tileDecorationOverhead;
	public Sprite[][] tileLayer;

	// Game Objects
	public World world;
	public GameObject board;
	public GameObject[,,] tiles;
	public Character hero;

	// Game Variables
	public string[] layers;
	public List<Character> characters;
	public Camera2D camera2D;
	public int smallWorldDrawOffsetX;
	public int smallWorldDrawOffsetY;
	public int turnsTaken;
	public float changeTileAnimateTimeLeft;

	// Moving transition variables
	public bool transitioning = false;
	public float transitionTimeLeft;
	public Direction transitionDirection;

	// Debug Variables
	public bool debugOn = false;
	public Color debugPathBlockColor = new Color(1, 0.6f, 0.6f, 1);

	/// <summary>
	/// Awake this instance, resources are loaded at this point.
	/// </summary>
	void Awake()
	{
		tileGround = Resources.LoadAll<Sprite>("tileGround");
		tileOverGround = Resources.LoadAll<Sprite>("tileOverGround");
		tileWall = Resources.LoadAll<Sprite>("tileWall");
		tileDecorationBase = Resources.LoadAll<Sprite>("tileDecorationBase");
		tileWallTableDecoration = Resources.LoadAll<Sprite>("tileWallTableDecoration");
		tileRoof = Resources.LoadAll<Sprite>("tileRoof");
		tileDecorationOverhead = Resources.LoadAll<Sprite>("tileDecorationOverhead");
		tileLayer = new Sprite[7][];
		tileLayer[0] = tileGround;
		tileLayer[1] = tileOverGround;
		tileLayer[2] = tileWall;
		tileLayer[3] = tileDecorationBase;
		tileLayer[4] = tileWallTableDecoration;
		tileLayer[5] = tileRoof;
		tileLayer[6] = tileDecorationOverhead;
		Character.charBase = Resources.LoadAll<Sprite>("charBase");
		Character.charHair = Resources.LoadAll<Sprite>("charHair");
		Character.charHead = Resources.LoadAll<Sprite>("charHead");
		Character.charLegs = Resources.LoadAll<Sprite>("charLegs");
		Character.charShield = Resources.LoadAll<Sprite>("charShield");
		Character.charTorso = Resources.LoadAll<Sprite>("charTorso");
		Character.charWeapon = Resources.LoadAll<Sprite>("charWeapon");
	}

	/// <summary>
	/// Start the game, this is used for initialization.
	/// </summary>
	void Start()
	{
		world = new World();
		camera2D = new Camera2D(world, world.spawnX, world.spawnY);
		smallWorldDrawOffsetX = (Camera2D.VIEW_TILES_WIDE - world.tilesWide) / 2;
		smallWorldDrawOffsetY = (Camera2D.VIEW_TILES_HIGH - world.tilesHigh) / 2;
		if (smallWorldDrawOffsetX < 0)
		{
			smallWorldDrawOffsetX = 0;
		}
		if (smallWorldDrawOffsetY < 0)
		{
			smallWorldDrawOffsetY = 0;
		}
		turnsTaken = 0;

		characters = new List<Character>();

		hero = new Character(world, world.spawnX, world.spawnY);
		characters.Add(hero);
		hero.baseType = Random.Range(0, Character.charBase.Length);
		hero.hairType = Random.Range(0, Character.charHair.Length);
		hero.legsType = Random.Range(0, Character.charLegs.Length);
		hero.torsoType = Random.Range(0, Character.charTorso.Length);
		hero.headType = Random.Range(0, Character.charHead.Length);
		hero.shieldType = Random.Range(0, Character.charShield.Length);
		hero.weaponType = Random.Range(0, Character.charWeapon.Length);

		for (int i = 0; i < 100; i++)
		{
			Character dummy = new Character(world, world.spawnX + (i % 10), world.spawnY + (i / 10));
			characters.Add(dummy);
			dummy.baseType = Random.Range(0, Character.charBase.Length);
			dummy.hairType = Random.Range(0, Character.charHair.Length);
		}

		layers = new string[]{
			"Ground",
			"OverGround",
			"Wall",
			"DecorationBase",
			"WallTableDecoration",
			"Roof",
			"DecorationOverhead"
		};

		// Setup the board
		board = new GameObject();
		board.name = "Board";
		tiles = new GameObject[layers.Length, Camera2D.VIEW_TILES_WIDE + 2, Camera2D.VIEW_TILES_HIGH + 2];

		// Add tiles for each layer on the main screen and just off screen for transitions
		for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
		{
			string layer = layers[layerIndex];
			for (int x = -1; x <= Camera2D.VIEW_TILES_WIDE; x++)
			{
				for (int y = -1; y <= Camera2D.VIEW_TILES_HIGH; y++)
				{
					// Don't tile the corners off screen
					if ((x == -1 || x == Camera2D.VIEW_TILES_WIDE) && (y == -1 || y == Camera2D.VIEW_TILES_HIGH))
					{
						continue;
					}
					GameObject tile = new GameObject();
					tile.AddComponent<SpriteRenderer>();
					tile.GetComponent<SpriteRenderer>().sortingLayerName = layer;
					tile.transform.position = new Vector3(x * 16, (y + 1) * 16, 0);
					tile.name = layer + "X-" + (x + 1) + "Y-" + (y + 1);
					tile.transform.parent = board.transform;
					tiles[layerIndex, x + 1, y + 1] = tile;
				}
			}
		}

		updateCharactersVisibility();
		updateCamera();
	}

	/// <summary>
	/// Update the game, this called once per frame.
	/// </summary>
	void Update()
	{
		if (transitioning)
		{
			handleTransition();
		}
		else
		{
			handleInput();
		}
		handleTileAnimation();
		drawWorld();
	}

	/// <summary>
	/// Handles the tile animations.
	/// </summary>
	private void handleTileAnimation()
	{
		changeTileAnimateTimeLeft -= Time.deltaTime;
		if (changeTileAnimateTimeLeft <= 0)
		{
			changeTileAnimateTimeLeft = ANIMATE_TILE_TIME;
			world.changeTileAnimations();
		}
	}

	/// <summary>
	/// Handles the transition between tiles when moving.
	/// </summary>
	private void handleTransition()
	{
		transitionTimeLeft -= Time.deltaTime;
		if (transitionTimeLeft <= 0)
		{
			transitioning = false;
			transitionDirection = Direction.NONE;
			foreach (Character character in characters)
			{
				character.finishTransition();
			}
			camera2D.finishTransition();
			board.transform.position = new Vector3(0, 0, 0);
		}
		updateCamera();
	}

	/// <summary>
	/// Handles the user input.
	/// </summary>
	private void handleInput()
	{
		// Below is for debug purposes
		if (Input.GetKeyDown(KeyCode.F))
		{
			debugOn = !debugOn;
		}
		if (Input.GetKeyDown(KeyCode.R))
		{
			world = new World();
			turnsTaken = 0;
			hero.x = hero.newX = world.spawnX;
			hero.y = hero.newY = world.spawnY;
			camera2D.focusX = camera2D.newFocusX = world.spawnX;
			camera2D.focusY = camera2D.newFocusY = world.spawnY;
			updateCharactersVisibility();
			updateCamera();
		}

		bool turnTaken = false;
		if (Input.GetKey(KeyCode.Q))
		{
			// Cancel, Open/Close Menu
		}
		if (Input.GetKey(KeyCode.E))
		{
			// Confirm
		}
		if (Input.GetKey(KeyCode.W))
		{
			// Movement up
			int newHeroY = Mathf.Min(world.tilesHigh - 1, hero.y + 1);
			if (!world.pathBlocked(hero.x, newHeroY) && newHeroY != hero.y)
			{
				turnTaken = true;
				transitioning = true;
				transitionDirection = Direction.UP;
				transitionTimeLeft = MOVING_TRANSITION_TIME;
			}
		}
		if (Input.GetKey(KeyCode.S))
		{
			// Movement down
			int newHeroY = Mathf.Max(0, hero.y - 1);
			if (!world.pathBlocked(hero.x, newHeroY) && newHeroY != hero.y)
			{
				turnTaken = true;
				transitioning = true;
				transitionDirection = Direction.DOWN;
				transitionTimeLeft = MOVING_TRANSITION_TIME;
			}
		}
		if (Input.GetKey(KeyCode.A))
		{
			// Movement left
			int newHeroX = Mathf.Max(0, hero.x - 1);
			if (!world.pathBlocked(newHeroX, hero.y) && newHeroX != hero.x)
			{
				turnTaken = true;
				transitioning = true;
				transitionDirection = Direction.LEFT;
				transitionTimeLeft = MOVING_TRANSITION_TIME;
			}
		}
		if (Input.GetKey(KeyCode.D))
		{
			// Movement right
			int newHeroX = Mathf.Min(world.tilesWide - 1, hero.x + 1);
			if (!world.pathBlocked(newHeroX, hero.y) && newHeroX != hero.x)
			{
				turnTaken = true;
				transitioning = true;
				transitionDirection = Direction.RIGHT;
				transitionTimeLeft = MOVING_TRANSITION_TIME;
			}
		}
		if (Input.GetMouseButtonDown(0))
		{
			hero.baseType = Random.Range(0, Character.charBase.Length);
			hero.hairType = Random.Range(0, Character.charHair.Length);
			hero.legsType = Random.Range(0, Character.charLegs.Length);
			hero.torsoType = Random.Range(0, Character.charTorso.Length);
			hero.headType = Random.Range(0, Character.charHead.Length);
			hero.shieldType = (Random.Range(0, 2) == 1 ? Random.Range(0, Character.charShield.Length) : -1);
			hero.weaponType = (Random.Range(0, 2) == 1 ? Random.Range(0, Character.charWeapon.Length) : -1);
		}
		if (turnTaken)
		{
			turnsTaken++;
		}
		if (transitioning)
		{
			camera2D.transitioning(transitionDirection);
			switch (transitionDirection)
			{
				case Direction.UP:
					hero.newY = hero.y + 1;
					break;
				case Direction.DOWN:
					hero.newY = hero.y - 1;
					break;
				case Direction.LEFT:
					hero.newX = hero.x - 1;
					break;
				case Direction.RIGHT:
					hero.newX = hero.x + 1;
					break;
			}
			updateCharactersVisibility();
		}
	}

	/// <summary>
	/// Updates the characters display visibility.
	/// </summary>
	private void updateCharactersVisibility()
	{
		foreach (Character character in characters)
		{
			if (((camera2D.xMin <= character.x) && (character.x <= camera2D.xMax)) && ((camera2D.yMin <= character.y) && (character.y <= camera2D.yMax)))
			{
				character.visible = true;
				character.container.transform.position = new Vector3(16 * (character.x + smallWorldDrawOffsetX - camera2D.xMin), 16 * (character.y + smallWorldDrawOffsetY - camera2D.yMin), 0);
			}
			else if (((camera2D.newXMin <= character.newX) && (character.newX <= camera2D.newXMax)) && ((camera2D.newYMin <= character.newY) && (character.newY <= camera2D.newYMax)))
			{
				character.visible = true;
				character.container.transform.position = new Vector3(16 * (character.x + smallWorldDrawOffsetX - camera2D.xMin), 16 * (character.y + smallWorldDrawOffsetY - camera2D.yMin), 0);
			}
			else
			{
				character.visible = false;
			}
		}
	}

	/// <summary>
	/// Updates the board and character positions on the screen.
	/// </summary>
	private void updateCamera()
	{
		if (!transitioning)
		{
			foreach (Character character in characters)
			{
				if (character.visible)
				{
					character.container.transform.position = new Vector3(16 * (character.x + smallWorldDrawOffsetX - camera2D.xMin), 16 * (character.y + smallWorldDrawOffsetY - camera2D.yMin), 0);
				}
			}
		}
		else
		{
			int cameraDiffX = camera2D.newXMin - camera2D.xMin;
			int cameraDiffY = camera2D.newYMin - camera2D.yMin;
			foreach (Character character in characters)
			{
				if (character.visible)
				{
					int diffX = character.newX - character.x - cameraDiffX;
					int diffY = character.newY - character.y - cameraDiffY;
					character.container.transform.position += (Vector3.right * Time.deltaTime) * diffX * 16 * (1f / MOVING_TRANSITION_TIME);
					character.container.transform.position += (Vector3.up * Time.deltaTime) * diffY * 16 * (1f / MOVING_TRANSITION_TIME);
				}
			}
			switch (transitionDirection)
			{
				case Direction.UP:
					if (!((camera2D.newYMin <= 0) || (camera2D.yMax >= (world.tilesHigh - 1))))
					{
						// Camera is not on the border
						board.transform.position += (Vector3.down * Time.deltaTime) * 16 * (1f / MOVING_TRANSITION_TIME);
					}
					break;
				case Direction.DOWN:
					if (!((camera2D.yMin <= 0) || (camera2D.newYMax >= (world.tilesHigh - 1))))
					{
						// Camera is not on the border
						board.transform.position += (Vector3.up * Time.deltaTime) * 16 * (1f / MOVING_TRANSITION_TIME);
					}
					break;
				case Direction.LEFT:
					if (!((camera2D.xMin <= 0) || (camera2D.newXMax >= (world.tilesWide - 1))))
					{
						// Camera is not on the border
						board.transform.position += (Vector3.right * Time.deltaTime) * 16 * (1f / MOVING_TRANSITION_TIME);
					}
					break;
				case Direction.RIGHT:
					if (!((camera2D.newXMin <= 0) || (camera2D.xMax >= (world.tilesWide - 1))))
					{
						// Camera is not on the border
						board.transform.position += (Vector3.left * Time.deltaTime) * 16 * (1f / MOVING_TRANSITION_TIME);
					}
					break;
			}
		}
	}

	/// <summary>
	/// Draws the world.
	/// </summary>
	private void drawWorld()
	{
		float tint = world.getDayTimeTint(turnsTaken, TURNS_PER_DAY);
		Color tintColor = new Color(tint, tint, tint, 1);
		// Loop through each layer, ground, overground etc.
		for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
		{
			string layer = layers[layerIndex];
			for (int x = -1; x <= Camera2D.VIEW_TILES_WIDE; x++)
			{
				for (int y = -1; y <= Camera2D.VIEW_TILES_HIGH; y++)
				{
					// Skip the corners off screen
					if ((x == -1 || x == Camera2D.VIEW_TILES_WIDE) && (y == -1 || y == Camera2D.VIEW_TILES_HIGH))
					{
						continue;
					}
					int tileType = world.getTile(layer, camera2D.xMin - smallWorldDrawOffsetX + x, camera2D.yMin - smallWorldDrawOffsetY + y);
					GameObject tile = tiles[layerIndex, x + 1, y + 1];
					if (tileType != -1)
					{
						int lightLevel = world.getLightLevel(camera2D.xMin - smallWorldDrawOffsetX + x, camera2D.yMin - smallWorldDrawOffsetY + y);
						tile.GetComponent<SpriteRenderer>().sprite = tileLayer[layerIndex][tileType];

						bool pathBlock = world.pathBlocked(camera2D.xMin - smallWorldDrawOffsetX + x, camera2D.yMin - smallWorldDrawOffsetY + y);
						if (pathBlock && debugOn)
						{
							tile.GetComponent<SpriteRenderer>().material.color = debugPathBlockColor;
						}
						else
						{
							if (lightLevel > 0 && world.getLightTint(lightLevel) > tint)
							{
								tile.GetComponent<SpriteRenderer>().material.color = world.getLightTintColor(lightLevel);
							}
							else
							{
								tile.GetComponent<SpriteRenderer>().material.color = tintColor;
							}
						}
					}
					else
					{
						tile.GetComponent<SpriteRenderer>().sprite = null;
					}
				}
			}
		}
	}

	/// <summary>
	/// Removes the object passed in.
	/// </summary>
	/// <param name="obj">Object to be removed.</param>
	public static void RemoveObject(Object obj)
	{
		Destroy(obj);
	}
}