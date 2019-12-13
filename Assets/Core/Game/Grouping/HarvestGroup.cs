﻿using FastCollections;
using RTSLockstep.Data;

namespace RTSLockstep
{
    public class HarvestGroup
    {
        private MovementGroup harvestMoveGroup;
        private RTSAgent currentTarget;

        public int IndexID { get; set; }

        private byte controllerID;

        private FastList<Harvest> harvesters;

        private bool _calculatedBehaviors;

        public void Initialize(Command com)
        {
            _calculatedBehaviors = false;
            controllerID = com.ControllerID;
            Selection selection = AgentController.InstanceManagers[controllerID].GetSelection(com);
            harvesters = new FastList<Harvest>(selection.selectedAgentLocalIDs.Count);

            if (com.TryGetData(out DefaultData target) && target.Is(DataType.UShort))
            {
                if (AgentController.TryGetAgentInstance((ushort)target.Value, out RTSAgent tempTarget))
                {
                    if (tempTarget.MyAgentType == AgentType.Resource
                        || tempTarget.MyAgentType == AgentType.Structure)
                    {
                        currentTarget = tempTarget;
                    }
                }
            }

            if (MovementGroupHelper.CheckValidAndAlert())
            {
                // create a movement group for harvesters based on the current project
                Command moveCommand = new Command(AbilityDataItem.FindInterfacer(typeof(Move)).ListenInputID)
                {
                    ControllerID = controllerID
                };

                moveCommand.Add(currentTarget.IsNotNull() ? currentTarget.Body.Position : Vector2d.zero);

                harvestMoveGroup = MovementGroupHelper.CreateGroup(moveCommand);
            }
        }

        public void LocalSimulate()
        {

        }

        public void LateSimulate()
        {
            if (harvesters.IsNull() || harvesters.Count == 0)
            {
                Deactivate();
            }
            else if (harvesters.Count > 0)
            {
                if (!_calculatedBehaviors)
                {
                    _calculatedBehaviors = CalculateAndExecuteBehaviors();
                }
            }
        }

        public void Add(Harvest harvester)
        {
            if (harvester.MyHarvestGroup.IsNotNull())
            {
                harvester.MyHarvestGroup.harvesters.Remove(harvester);
            }
            harvester.MyHarvestGroup = this;
            harvester.MyHarvestGroupID = IndexID;

            harvesters.Add(harvester);

            // add the harvester to our harvester move group too!
            harvestMoveGroup.Add(harvester.Agent.MyStats.CachedMove);
        }

        public void Remove(Harvest harvester)
        {
            if (harvester.MyHarvestGroup.IsNotNull() && harvester.MyHarvestGroupID == IndexID)
            {
                harvesters.Remove(harvester);
                harvester.MyHarvestGroup = null;
                harvester.MyHarvestGroupID = -1;

                // Remove the harvester from our harvester move group too!
                harvestMoveGroup.Remove(harvester.Agent.MyStats.CachedMove);
            }
        }

        private bool CalculateAndExecuteBehaviors()
        {
            if (currentTarget.MyAgentType == AgentType.Resource)
            {
                ExecuteHarvest();
            }
            else if (currentTarget.MyAgentType == AgentType.Structure)
            {
                ExecuteDeposit();
            }

            return true;
        }

        private void Deactivate()
        {
            Harvest harvester;
            for (int i = 0; i < harvesters.Count; i++)
            {
                harvester = harvesters[i];
                harvester.MyHarvestGroup = null;
                harvester.MyHarvestGroupID = -1;
            }
            harvesters.FastClear();
            currentTarget = null;
            harvestMoveGroup = null;
            HarvestGroupHelper.Pool(this);
            _calculatedBehaviors = false;
            IndexID = -1;
        }

        private void ExecuteHarvest()
        {
            for (int i = 0; i < harvesters.Count; i++)
            {
                Harvest harvester = harvesters[i];
                harvester.IsHarvesting = true;
                harvester.IsEmptying = false;
                harvester.OnHarvestGroupProcessed(currentTarget);
            }
        }

        private void ExecuteDeposit()
        {
            for (int i = 0; i < harvesters.Count; i++)
            {
                Harvest harvester = harvesters[i];
                harvester.IsHarvesting = false;
                harvester.IsEmptying = true;
                harvester.OnHarvestGroupProcessed(currentTarget);
            }
        }
    }
}
