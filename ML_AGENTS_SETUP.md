# ML-Agents Robot Sumo Training Setup

## Files Created

1. **RobotAgent.cs** - ML-Agents script for robot control and learning
2. **SumoArenaManager.cs** - Manages the arena and episodes
3. **robot_sumo_config.yaml** - Training configuration for ML-Agents

## Unity Setup Instructions

### Step 1: Import ML-Agents Package

1. Open Unity Editor
2. Go to **Window → Package Manager**
3. Click the **+** button → **Add package from git URL**
4. Enter: `com.unity.ml-agents`
5. Click **Add**

### Step 2: Set Up Your Scene

#### Create the Arena:
1. Create a Platform:
   - Right-click in Hierarchy → **3D Object → Cube**
   - Rename to "Platform"
   - Scale: `(20, 1, 20)` for a 20x20 platform
   - Position: `(0, 0, 0)`

2. Create Robots:
   - Place your two robot models in the scene
   - Name them "Robot1" and "Robot2"
   - Ensure each has a **Collider** component

#### Set Up Arena Manager:
1. Create empty GameObject: **GameObject → Create Empty**
2. Rename to "ArenaManager"
3. Add the **SumoArenaManager** script to it
4. In Inspector:
   - Drag **Robot1** to "Robot1 Agent" slot (will fill after next step)
   - Drag **Robot2** to "Robot2 Agent" slot (will fill after next step)
   - Drag **Platform** to "Platform" slot
   - Set **Platform Radius** to `10` (half of platform size)
   - Set **Fall Height** to `-2`

#### Set Up Agents:
1. Select **Robot1** in Hierarchy
2. **Remove** the `WASDMovement` script (if attached)
3. Add **RobotAgent** component:
   - Click **Add Component** → Search "RobotAgent"
4. Configure RobotAgent:
   - **Robot Body**: Drag Robot1 itself (or its main body part)
   - **Opponent Agent**: Leave empty for now
   - **Arena**: Drag the Platform
   - Adjust movement speeds if needed

5. Repeat for **Robot2**:
   - Remove `robot2.cs` script
   - Add **RobotAgent** component
   - Configure same as Robot1

6. Add **Behavior Parameters** to both robots:
   - Click **Add Component** → "Behavior Parameters"
   - **Behavior Name**: `RobotAgent` (must match config file!)
   - **Vector Observation Space Size**: `18`
   - **Actions**:
     - **Continuous Actions**: `2`
     - **Discrete Branches**: (leave empty)
   - **Behavior Type**: `Default` (for training)

7. Add **Decision Requester** to both robots:
   - Click **Add Component** → "Decision Requester"
   - **Decision Period**: `5` (requests decision every 5 frames)

8. Go back to **ArenaManager**:
   - Now drag Robot1's **RobotAgent** component to "Robot1 Agent"
   - Drag Robot2's **RobotAgent** component to "Robot2 Agent"

### Step 3: Configure Project Settings

1. Go to **Edit → Project Settings**
2. Select **Player**
3. Scroll to **Other Settings**
4. Change **API Compatibility Level** to `.NET 4.x` or `.NET Standard 2.1`

### Step 4: Start Training

#### Training in Unity Editor (Recommended for testing):

1. Open Terminal
2. Navigate to your project:
   ```bash
   cd "/Users/sb2/ROBOT GAME"
   ```

3. Activate virtual environment:
   ```bash
   source myvenv/bin/activate
   ```

4. Start training:
   ```bash
   mlagents-learn robot_sumo_config.yaml --run-id=robot_sumo_v1
   ```

5. When you see "Start training by pressing Play in Unity Editor", press **Play** in Unity

#### Training with Built Executable (Faster):

1. Build your game:
   - **File → Build Settings**
   - Platform: **Mac** (or your platform)
   - Click **Build** → Save as "RobotSumo.app"

2. Train with executable:
   ```bash
   mlagents-learn robot_sumo_config.yaml --run-id=robot_sumo_v1 --env="/Users/sb2/ROBOT GAME/RobotSumo.app"
   ```

### Step 5: Monitor Training

1. **Watch Unity**: See robots learning in real-time
2. **Check Terminal**: See training progress, rewards
3. **TensorBoard** (optional):
   ```bash
   tensorboard --logdir results
   ```
   Open browser to `http://localhost:6006`

### Step 6: Test Trained Model

1. Stop training (Ctrl+C)
2. In Unity, select both robots
3. Change **Behavior Parameters → Behavior Type** to `Inference Only`
4. In **Behavior Parameters**, drag the trained model:
   - Find in `results/robot_sumo_v1/RobotAgent.onnx`
   - Drag to **Model** slot
5. Press Play to watch trained agents battle!

## Training Configuration Explained

The `robot_sumo_config.yaml` uses:
- **Self-Play**: Agents train against each other (competitive)
- **PPO**: Proximal Policy Optimization algorithm
- **5M steps**: About 5-10 hours of training
- **Rewards**:
  - Win: +1.0
  - Lose: -1.0
  - Pushing opponent: +0.05/step
  - Moving forward: +0.002/step
  - Facing opponent: +0.005/step

## Adjusting Training

### Make training faster:
```yaml
max_steps: 1000000  # 1M instead of 5M
time_horizon: 500   # Shorter episodes
```

### Increase exploration:
```yaml
beta: 1.0e-2  # Higher entropy
```

### Add curiosity (helps learning):
Uncomment the curiosity section in the YAML

## Troubleshooting

**"No module named mlagents":**
```bash
pip install mlagents
```

**"Behavior name not found":**
- Make sure Behavior Parameters → Behavior Name is exactly `RobotAgent`

**Robots don't move:**
- Check Decision Requester is attached
- Check Behavior Type is `Default` during training

**Training seems stuck:**
- Watch cumulative reward in terminal
- Should increase over time
- May take 100k+ steps to see improvement

## Next Steps

1. Set up the scene as described above
2. Start with editor training for 100k steps to test
3. If working well, build executable for faster training
4. Train for full 5M steps
5. Test trained agents in inference mode
6. Adjust rewards/config if needed and retrain

Good luck training your robot sumo agents! 🤖
