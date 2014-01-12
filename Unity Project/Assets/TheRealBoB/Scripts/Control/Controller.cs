using UnityEngine;
using System;
using System.Collections.Generic;
using Algorithms;

public class Controller
{
	Model model;
	BView bView;

	public Controller (BView bView)
	{
		this.bView = bView;
		Init();
	}

	void Init() {
		Database.LoadFromFile();
		MapData mapData = Database.GetMapData();

        EventProxyManager.RegisterForEvent(EventName.RoundSetup, HandleRoundSetup);
		EventProxyManager.RegisterForEvent(EventName.UnitDied, HandleUnitDied);
		EventProxyManager.RegisterForEvent(EventName.TurnStarted, HandleTurnStarted);
		
		model = new Model(this);
		model.InitMap(mapData);
		model.InitUnits(mapData);
		model.InitCombat();
	}

	#region event handler
    void HandleRoundSetup(object sender, EventArgs args)
    {
        // after round setup start it
		new CStartTurn(model,this).Execute();
    }

	void HandleUnitDied (object sender, EventArgs args)
	{
		new CGameover(model).Execute();
	}
	
	void HandleMapTileTapped(object sender, EventArgs args)
	{
		Debug.Log("Tap Event triggert");
	}

	void HandleTurnStarted (object sender, EventProxyArgs args)
	{
		if(!model.matchRunning)
			return;

		TurnStartedEvent e = args as TurnStartedEvent;
		if(e.unit.AIControled) {
			e.unit.ai.PlanTurn();

			MoveUnit(e.unit, e.unit.ai.MoveDestionation);
			AttackUnit(e.unit, e.unit.ai.AttackTarget, e.unit.ai.AttackChoice);
		}
	}
	#endregion

	public void MoveUnit(Unit unit, MapTile mapTile)
	{
		new CMoveUnit(model,unit,mapTile, this).Execute();
	}

	public void AttackUnit(Unit source, Unit target, Attack attack )
	{
		new CAttackUnit(source,target,attack, this).Execute();

		EndTurn();
	}

	public void EndTurn()
	{
		if (model.combat.TurnsLeft() > 0)
			new CStartTurn(model,this).Execute ();
		else
			model.combat.SetupRound (model.units);
	}

	public byte[][] GetDistanceMatrix(Point position, int actionPoints, bool ignoreUnits)
	{
		byte[,] grid;
		if(ignoreUnits) {
			grid = model.GetAttackGrid();
		} else {
			grid = model.GetMoveGrid();
		}
		PathFinder pathFinder = new PathFinder(grid);
		pathFinder.Diagonals = false;
		pathFinder.PunishChangeDirection = false;
		return pathFinder.GetDistanceMatrix(position, actionPoints);
	}

	public Path GetPath(MapTile start, MapTile goal)
	{
		byte[,] grid = model.GetMoveGrid();
		
		// check if target mapTile is passable
		if(grid[goal.x,goal.y] == 0) {
			Debug.LogError("Tried to get path to a taken mapTile: " + start.ToString() + " --> " + goal.ToString());
			return new Path();
		}

		PathFinder pathFinder = new PathFinder(grid);
		pathFinder.Diagonals = false;
		pathFinder.PunishChangeDirection = false;
		
		Point startPoint = new Point(start.x,start.y);
		Point endPoint = new Point(goal.x, goal.y);
		
		List<PathFinderNode> result = pathFinder.FindPath(startPoint, endPoint);
		Path path = GeneratePathObject(result);

		if(path.Empty) {
			Debug.LogError("Caluclated path is empty; i.e. there is no path for the given parameters! \n" + start.ToString() + " --> " + goal.ToString() + "\n" + grid);
		}
		return path;
	}

	public Path GeneratePathObject(List<PathFinderNode> path)
	{
		MapTile[] mapTilePath = new MapTile[path.Count];
		
		for (int i = 0; i < path.Count; i++) {
			mapTilePath[i] = model.mapTiles[path[i].X][path[i].Y];	
		}

		return new Path(mapTilePath, GetPathCost(mapTilePath));
	}

	public int GetPathCost (MapTile[] path)
	{
		int cost = 0;
		foreach(MapTile mapTile in path) {
			cost += mapTile.Penalty;
		}
		// don't include the first MapTile; Unit already sits on this MapTile
		cost -= path[0].Penalty;
		
		return cost;
	}
}

