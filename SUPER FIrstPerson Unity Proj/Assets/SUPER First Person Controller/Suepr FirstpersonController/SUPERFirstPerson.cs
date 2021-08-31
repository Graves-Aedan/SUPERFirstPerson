//Original Code Author: Aedan Graves

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;
#endif

///TODO
// Better implement the new input system.
// create compatibility layers for Unity 2017 and 2018
// better implement animation calls(?)
// more camera animations

[RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(CapsuleCollider))]
public class SUPERFirstPerson : MonoBehaviour
{
    #region Camera Settings
    [Header("Camera Settings")]
    //
    //Public
    //
    //Both
    public Camera playerCamera;
    public bool lockAndHideMouse = true, autoGenerateCrosshair = true, showCrosshairIn3rdPerson = false;
    public Sprite crosshairSprite;
    public PerspectiveModes cameraPerspective = PerspectiveModes._1stPerson;
    //use mouse wheel to switch modes. (too close will set it to fps mode and attempting to zoom out from fps will switch to tps mode)
    public bool automaticallySwitchPerspective = true;
    #if ENABLE_INPUT_SYSTEM
    public Key perspectiveSwitchingKey = Key.None;
    #else
    public KeyCode perspectiveSwitchingKey = KeyCode.None;
    #endif

    public MouseInputInversionModes mouseInputInversion;
    [Range(1,20)] public float Sensitivity = 8;
    [Range(1f,25)]public float rotationWeight = 4;
    [Range(1.0f,180.0f)] public float verticalRotationRange = 170.0f;
    [Range(0,1)] public float eyeHeight = 0.8f;

    //First person
    public ViewInputModes viewInputMethods;
    [Range(0,50)]public float FOVKickAmount = 10; 
    [Range(0,1)] public float FOVSensitivityMultiplier = 0.74f;

    //Third Person
    public bool rotateCharaterToCameraForward = false;
    [Range(0,15)] public float maxCameraDistance = 8;
    public LayerMask cameraObstructionIgnore = -1;
    [Range(1,5)] public float cameraZoomSensitivity = 5; 


    //
    //Internal
    //
    
    //Both
    Vector2 MouseXY;
    Vector2 viewRotVelRef;
    bool isInFirstPerson, isInThirdPerson, perspecTog;
    Image crosshairImg;
    //First Person
    float initialCameraFOV, FOVKickVelRef, currentFOVMod;

    //Third Person
    float mouseScrollWheel, maxCameraDistInternal, currentCameraZ, cameraZRef;
    Vector3 headPos, headRot, currentCameraPos, cameraOutPos, cameraPosVelRef;
    Quaternion quatHeadRot;
    Ray cameraObstCheck;
    RaycastHit cameraObstResult;
    [Space(20)]
    #endregion

    #region Movement
    [Header("Movement Settings")]
    
    //
    //Public
    //

    //Walking/Sprinting/Crouching
    [Range(1.0f,650.0f)]public float walkingSpeed = 140, sprintingSpeed = 260, crouchingSpeed = 45;
    [Range(1.0f,400.0f)] public float decelerationSpeed=240;
    #if ENABLE_INPUT_SYSTEM
    public Key sprintKey = Key.LeftShift, crouchKey = Key.LeftCtrl;
    #else
    public KeyCode sprintKey = KeyCode.LeftShift, crouchKey = KeyCode.LeftControl;
    #endif
    public bool canSprint=true, isSprinting, toggleSprint, sprintOveride, canCrouch=true, isCrouching, toggleCrouch, crouchOverride, isIdle;
    public Stances currentStance = Stances.Standing;
    public float stanceTransisionSpeed = 5.0f, crouchingHeight = 0.80f;
    public GroundSpeedProfiles currentGroundMovementSpeed = GroundSpeedProfiles.Walking;
    public LayerMask whatIsGround;

    //Slope affectors
    public float hardSlopeLimit = 70, slopeInfluenceOnSpeed = 1, maxStairRise = 0.25f, minimumTreadDepth=0.2f;

    //Jumping
    public bool canJump=true,holdJump=false,Jumped;
    #if ENABLE_INPUT_SYSTEM
        public Key jumpKey = Key.Space;
    #else
        public KeyCode jumpKey = KeyCode.Space;
    #endif
    [Range(1.0f,650.0f)] public float jumpPower = 40;
    [Range(0.0f,1.0f)] public float airControlFactor = 1;

    float jumpBlankingPeriod;

    //Sliding
    public bool isSliding, canSlide = true;
    public float slidingDeceleration = 150.0f, slidingTransisionSpeed=4, maxFlatSlideDistance =10;
    

    //
    //Internal
    //

    //Walking/Sprinting/Crouching
    public GroundInfo currentGroundInfo = new GroundInfo();
    float standingHeight;
    float currentGroundSpeed;
    Vector3 InputDir;
    Vector2 MovInput;
    Vector2 _2DVelocity;
    float _2DVelocityMag, speedToVelocityRatio;
    PhysicMaterial _ZeroFriction, _MaxFriction;
    CapsuleCollider capsule;
    Rigidbody p_Rigidbody;
    bool crouchInput,sprintInput, crouchInputDown, sprintInputDown;

    //Slope Affectors

    //Jumping
    bool jumpInput;

    //Sliding
    Vector3 cachedDirPreSlide, cachedPosPreSlide;



    [Space(20)]
    #endregion
    
    #region Parkour
    //
    //Public
    //

    //Vaulting
    public bool canVault = true, isVaulting, autoVaultWhenSpringing;
    #if ENABLE_INPUT_SYSTEM
    public Key VaultKey = Key.E;
    #else
    public KeyCode VaultKey = KeyCode.E;
    #endif
    public string vaultObjectTag = "Vault Obj";
    public float vaultSpeed = 7.5f, maxVaultDepth = 1.5f, maxVaultHeight = 0.75f;


    //
    //Internal
    //

    //Vaulting
    RaycastHit VC_Stage1, VC_Stage2, VC_Stage3, VC_Stage4;
    Vector3 vaultForwardVec;
    bool vaultInput;

    //All
    private bool doingPosInterp, doingCamInterp;
    #endregion

    #region Stamina System
    //Public
    public bool enableStaminaSystem = true, jumpingDepletesStamina = true;
    [Range(0.0f,250.0f)]public float Stamina = 50.0f, currentStaminaLevel = 0, S_minimumStaminaToSprint = 5.0f, s_depletionSpeed = 2.0f,  s_regenerationSpeed = 1.2f, s_JumpStaminaDepletion = 5.0f;
    
    //Internal
    bool ignoreStamina = false;
    #endregion
    
    #region Footstep System
    [Header("Footstep System")]
    public bool enableFootstepSounds = true;
    public FootstepTriggeringMode footstepTriggeringMode = FootstepTriggeringMode.calculatedTiming;
    [Range(0.0f,1.0f)] public float stepTiming = 0.15f;
    public List<GroundMaterialProfile> footstepSoundSet = new List<GroundMaterialProfile>();
    bool shouldCalculateFootstepTriggers= true;
    float StepCycle = 0;
    AudioSource playerAudioSource;
    List<AudioClip> currentClipSet = new List<AudioClip>();
    [Space(18)]
    #endregion
    
    #region  Headbob
    //
    //Public
    //
    public bool enableHeadbob = true;
    [Range(1.0f,5.0f)] public float headbobSpeed = 0.5f, headbobPower = 0.25f;
    [Range(0.0f,3.0f)] public float ZTilt = 3;

    //
    //Internal
    //
    bool shouldCalculateHeadbob;
    Vector3 headbobCameraPosition;
    float headbobCyclePosition, headbobWarmUp;

    #endregion
    
    #region  Survival Stats
    //
    //Public
    //
    public bool enableSurvivalStats = true;
    public SurvivalStats defaultSurvivalStats = new SurvivalStats();
    public float statTickRate = 6.0f, hungerDepletionRate = 0.06f, hydrationDepletionRate = 0.14f;
    public SurvivalStats currentSurvivalStats = new SurvivalStats();

    //
    //Internal
    //
    float StatTickTimer;
    #endregion

    #region Animation
    //
    //Pulbic
    //

    //Firstperson
    public Animator _1stPersonCharacterAnimator;
    //ThirdPerson
    public Animator _3rdPersonCharacterAnimator;

    #endregion

    [Space(18)]
    public bool enableGroundingDebugging = false, enableMovementDebugging = false, enableMouseAndCameraDebugging = false, enableVaultDebugging = false;

    void Start()
    {
   
        
        
        #region Camera
        maxCameraDistInternal = maxCameraDistance;
        initialCameraFOV = playerCamera.fieldOfView;
        headbobCameraPosition = Vector3.up*eyeHeight;
        if(lockAndHideMouse){
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if(autoGenerateCrosshair && crosshairSprite){
            Canvas canvas = playerCamera.gameObject.GetComponentInChildren<Canvas>();
            if(canvas == null){canvas = new GameObject("AutoCrosshair").AddComponent<Canvas>();}
            canvas.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.transform.SetParent(playerCamera.transform);
            canvas.transform.position = Vector3.zero;
            crosshairImg = new GameObject("Crosshair").AddComponent<Image>();
            crosshairImg.sprite = crosshairSprite;
            crosshairImg.rectTransform.sizeDelta = new Vector2(25,25);
            crosshairImg.transform.SetParent(canvas.transform);
            crosshairImg.transform.position = Vector3.zero;
            crosshairImg.raycastTarget = false;
            if(cameraPerspective == PerspectiveModes._3rdPerson && !showCrosshairIn3rdPerson){
                crosshairImg.gameObject.SetActive(false);
            }
        }
        #endregion 

        #region Movement
        p_Rigidbody = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        standingHeight = capsule.height;
        currentGroundSpeed = walkingSpeed;
        _ZeroFriction = new PhysicMaterial("Zero_Friction");
        _ZeroFriction.dynamicFriction =0f;
        _ZeroFriction.staticFriction =0;
        _ZeroFriction.frictionCombine = PhysicMaterialCombine.Minimum;
        _ZeroFriction.bounceCombine = PhysicMaterialCombine.Minimum;
        _MaxFriction = new PhysicMaterial("Max_Friction");
        _MaxFriction.dynamicFriction =1;
        _MaxFriction.staticFriction =1;
        _MaxFriction.frictionCombine = PhysicMaterialCombine.Maximum;
        _MaxFriction.bounceCombine = PhysicMaterialCombine.Average;
        #endregion

        #region Stamina System
        currentStaminaLevel = Stamina;
        #endregion
        
        #region Footstep
        playerAudioSource = GetComponent<AudioSource>();
        #endregion
        
    }
    void Update()
    {

        #region Input
        #if ENABLE_INPUT_SYSTEM
            MouseXY.x = Mouse.current.delta.y.ReadValue()/50;
            MouseXY.y = Mouse.current.delta.x.ReadValue()/50;
            
            mouseScrollWheel = Mouse.current.scroll.y.ReadValue()/50;
            perspecTog = Keyboard.current.qKey.wasPressedThisFrame;
            
            //movement

            jumpInput =  (holdJump? Keyboard.current.spaceKey.isPressed : Keyboard.current.spaceKey.wasPressedThisFrame);
            crouchInput =  Keyboard.current.leftCtrlKey.isPressed;
            crouchInputDown = Keyboard.current.leftCtrlKey.wasPressedThisFrame;
            sprintInput = Keyboard.current.leftShiftKey.isPressed;
            sprintInputDown = Keyboard.current.leftShiftKey.wasPressedThisFrame;
            vaultInput = Keyboard.current.eKey.isPressed;
            MovInput.x = Keyboard.current.aKey.isPressed ? -1 : Keyboard.current.dKey.isPressed ? 1 : 0;
            MovInput.y = Keyboard.current.wKey.isPressed ? 1 : Keyboard.current.sKey.isPressed ? -1 : 0;
        #else
            //camera
            MouseXY.x = Input.GetAxis("Mouse Y");
            MouseXY.y = Input.GetAxis("Mouse X");
            mouseScrollWheel = Input.GetAxis("Mouse ScrollWheel");
            perspecTog = Input.GetKeyDown(perspectiveSwitchingKey);
            
            //movement

            jumpInput = (holdJump? Input.GetKey(jumpKey):Input.GetKeyDown(jumpKey));
            crouchInput = Input.GetKey(crouchKey);
            crouchInputDown = Input.GetKeyDown(crouchKey);
            sprintInput = Input.GetKey(sprintKey);
            sprintInputDown = Input.GetKeyDown(sprintKey);
            vaultInput = Input.GetKeyDown(VaultKey);
            MovInput = Vector2.up *Input.GetAxisRaw("Vertical") + Vector2.right * Input.GetAxisRaw("Horizontal");
        #endif
        #endregion

        #region Camera
        switch (cameraPerspective){
            case PerspectiveModes._1stPerson:{
                if(!isInFirstPerson){ChangePerspective(PerspectiveModes._1stPerson);}
                if(perspecTog||(automaticallySwitchPerspective&&mouseScrollWheel<0)){ ChangePerspective(PerspectiveModes._3rdPerson); }
                    HeadbobCycleCalculator();
                FOVKick();
            }break;

            case PerspectiveModes._3rdPerson:{
                if(!isInThirdPerson){ChangePerspective(PerspectiveModes._3rdPerson);}
                if(perspecTog||(automaticallySwitchPerspective&&maxCameraDistInternal ==0 &&currentCameraZ == 0)){ChangePerspective(PerspectiveModes._1stPerson); }
                maxCameraDistInternal = Mathf.Clamp(maxCameraDistInternal - (mouseScrollWheel*(cameraZoomSensitivity*2)),automaticallySwitchPerspective ? 0 : (capsule.radius*2),maxCameraDistance);
            }break;
        }

        RotateView(MouseXY, Sensitivity, rotationWeight);
        #endregion

        #region Movement
        InputDir = cameraPerspective == PerspectiveModes._1stPerson?  Vector3.ClampMagnitude((transform.forward*MovInput.y+transform.right * (viewInputMethods == ViewInputModes.Traditional ? MovInput.x : 0)),1) : Quaternion.AngleAxis(headRot.y,Vector3.up) * (Vector3.ClampMagnitude((Vector3.forward*MovInput.y+Vector3.right * MovInput.x),1));
        GroundMovementSpeedUpdate();
        if(canJump&& jumpInput){Jump(jumpPower);}
        #endregion
        
        #region Stamina system
        if(enableStaminaSystem){CalculateStamina();}
        #endregion

        #region Footstep
        CalculateFootstepTriggers();
        #endregion
    
        #region Parkour
        #endregion

        #region Survival Stats
        if(enableSurvivalStats && Time.time > StatTickTimer){
            TickStats();
        }
        #endregion

        #region Animation
        UpdateAnimationTriggers();
        #endregion
    }
    void FixedUpdate() {

        #region Movement
        GetGroundInfo();
        MovePlayer(InputDir,currentGroundSpeed);
        if(isSliding){Slide();}
        #endregion

        #region Camera
        if(cameraPerspective == PerspectiveModes._3rdPerson){
            UpdateCameraPosition_3rdPerson();
        }
        #endregion
    }

 
    #region Camera Functions
    void RotateView(Vector2 yawPitchInput, float inputSensitivity, float cameraWeight){

        switch (viewInputMethods){
            
            case ViewInputModes.Traditional:{  
                yawPitchInput.x *= ((mouseInputInversion==MouseInputInversionModes.X||mouseInputInversion == MouseInputInversionModes.Both) ? 1 : -1);
                yawPitchInput.y *= ((mouseInputInversion==MouseInputInversionModes.Y||mouseInputInversion == MouseInputInversionModes.Both) ? -1 : 1);
                switch(cameraPerspective){
                    case PerspectiveModes._1stPerson:{
                        Vector2 targetAngles = ((Vector2.right*playerCamera.transform.localEulerAngles.x)+(Vector2.up*transform.localEulerAngles.y));
                        float fovMod = FOVSensitivityMultiplier>0 && playerCamera.fieldOfView <= initialCameraFOV ? ((initialCameraFOV - playerCamera.fieldOfView)*(FOVSensitivityMultiplier/10))+1 : 1;
                        targetAngles = Vector2.SmoothDamp(targetAngles, targetAngles+(yawPitchInput*((inputSensitivity/fovMod)*Mathf.Pow(cameraWeight*fovMod,2))), ref viewRotVelRef,(Mathf.Pow(cameraWeight*fovMod,2))*Time.deltaTime);
                        targetAngles.x += targetAngles.x>180 ? -360 : targetAngles.x<-180 ? 360 :0;
                        targetAngles.x = Mathf.Clamp(targetAngles.x,-0.5f*verticalRotationRange,0.5f*verticalRotationRange);
                        playerCamera.transform.localEulerAngles = (Vector3.right * targetAngles.x) + (Vector3.forward* (enableHeadbob? headbobCameraPosition.z : 0));
                        transform.localEulerAngles = (Vector3.up*targetAngles.y);
                    }break;

                    case PerspectiveModes._3rdPerson:{
                        headPos = p_Rigidbody.position + Vector3.up *eyeHeight;
                        quatHeadRot = Quaternion.Euler(headRot);
                        headRot = Vector3.SmoothDamp(headRot,headRot+((Vector3)yawPitchInput*(inputSensitivity*Mathf.Pow(cameraWeight,2))),ref cameraPosVelRef ,(Mathf.Pow(cameraWeight,2))*Time.deltaTime);
                        headRot.y += headRot.y>180 ? -360 : headRot.y<-180 ? 360 :0;
                        headRot.x += headRot.x>180 ? -360 : headRot.x<-180 ? 360 :0;
                        headRot.x = Mathf.Clamp(headRot.x,-0.5f*verticalRotationRange,0.5f*verticalRotationRange);
                        cameraObstCheck= new Ray(headPos+(quatHeadRot*(Vector3.forward*capsule.radius)), quatHeadRot*-Vector3.forward);
                        if(enableMouseAndCameraDebugging){
                            Debug.Log(headRot);
                            Debug.DrawRay(cameraObstCheck.origin,cameraObstCheck.direction*Vector3.Distance(headPos,Vector3.forward*currentCameraZ),Color.red);
                        }   
                        if(Physics.Raycast(cameraObstCheck, out cameraObstResult,maxCameraDistInternal, cameraObstructionIgnore,QueryTriggerInteraction.Ignore)){
                            currentCameraZ = -(Vector3.Distance(headPos,cameraObstResult.point)*0.9f);
                        }else{
                            currentCameraZ = Mathf.SmoothDamp(currentCameraZ, -(maxCameraDistInternal*0.85f), ref cameraZRef ,Time.deltaTime,10);
                        }
                    }break;
                        
                }
            
            }break;
            
            case ViewInputModes.Retro:{
                yawPitchInput = Vector2.up * (Input.GetAxis("Horizontal") * ((mouseInputInversion==MouseInputInversionModes.Y||mouseInputInversion == MouseInputInversionModes.Both) ? -1 : 1));
                Vector2 targetAngles = ((Vector2.right*playerCamera.transform.localEulerAngles.x)+(Vector2.up*transform.localEulerAngles.y));
                float fovMod = FOVSensitivityMultiplier>0 && playerCamera.fieldOfView <= initialCameraFOV ? ((initialCameraFOV - playerCamera.fieldOfView)*(FOVSensitivityMultiplier/10))+1 : 1;
                targetAngles = targetAngles+(yawPitchInput*((inputSensitivity/fovMod)));   
                targetAngles.x = 0;
                playerCamera.transform.localEulerAngles = (Vector3.right * targetAngles.x) + (Vector3.forward* (enableHeadbob? headbobCameraPosition.z : 0));
                transform.localEulerAngles = (Vector3.up*targetAngles.y);
            }break;
        }
        
    }
    public void RotateView(Vector3 AbsoluteEulerAngles, bool SmoothRotation){

        switch (cameraPerspective){

            case (PerspectiveModes._1stPerson):{
                AbsoluteEulerAngles.x += AbsoluteEulerAngles.x>180 ? -360 : AbsoluteEulerAngles.x<-180 ? 360 :0;
                AbsoluteEulerAngles.x = Mathf.Clamp(AbsoluteEulerAngles.x,-0.5f*verticalRotationRange,0.5f*verticalRotationRange);
                

                if(SmoothRotation){
                    IEnumerator SmoothRot(){
                        doingCamInterp = true;
                        Vector3 refVec = Vector3.zero, targetAngles = (Vector3.right * playerCamera.transform.localEulerAngles.x)+Vector3.up*transform.eulerAngles.y;
                        while(Vector3.Distance(targetAngles, AbsoluteEulerAngles)>0.1f){ 
                            targetAngles = Vector3.SmoothDamp(targetAngles, AbsoluteEulerAngles, ref refVec, 25*Time.deltaTime);
                            targetAngles.x += targetAngles.x>180 ? -360 : targetAngles.x<-180 ? 360 :0;
                            targetAngles.x = Mathf.Clamp(targetAngles.x,-0.5f*verticalRotationRange,0.5f*verticalRotationRange);
                            playerCamera.transform.localEulerAngles = Vector3.right * targetAngles.x;
                            transform.eulerAngles = Vector3.up*targetAngles.y;
                            yield return null;
                        }
                        doingCamInterp =false;
                    }   
                    StopCoroutine("SmoothRot");
                    StartCoroutine(SmoothRot());
                }else{
                    playerCamera.transform.eulerAngles = Vector3.right * AbsoluteEulerAngles.x;
                    transform.eulerAngles = (Vector3.up*AbsoluteEulerAngles.y)+(Vector3.forward*AbsoluteEulerAngles.z);
                }
            }break;

            case (PerspectiveModes._3rdPerson):{
                if(SmoothRotation){
                    AbsoluteEulerAngles.y += AbsoluteEulerAngles.y>180 ? -360 : AbsoluteEulerAngles.y<-180 ? 360 :0;
                    AbsoluteEulerAngles.x += AbsoluteEulerAngles.x>180 ? -360 : AbsoluteEulerAngles.x<-180 ? 360 :0;
                    AbsoluteEulerAngles.x = Mathf.Clamp(AbsoluteEulerAngles.x,-0.5f*verticalRotationRange,0.5f*verticalRotationRange);
                    IEnumerator SmoothRot(){
                        doingCamInterp = true;
                        Vector3 refVec = Vector3.zero;
                        while(Vector3.Distance(headRot, AbsoluteEulerAngles)>0.1f){
                            headPos = p_Rigidbody.position + Vector3.up *eyeHeight;
                            quatHeadRot = Quaternion.Euler(headRot);
                            headRot = Vector3.SmoothDamp(headRot,AbsoluteEulerAngles,ref refVec ,25*Time.deltaTime);
                            headRot.y += headRot.y>180 ? -360 : headRot.y<-180 ? 360 :0;
                            headRot.x += headRot.x>180 ? -360 : headRot.x<-180 ? 360 :0;
                            headRot.x = Mathf.Clamp(headRot.x,-0.5f*verticalRotationRange,0.5f*verticalRotationRange);
                            yield return null;
                        }
                        doingCamInterp = false;
                    }
                    StopCoroutine("SmoothRot");
                    StartCoroutine(SmoothRot());
                }
                else{
                    headRot = AbsoluteEulerAngles;
                    headRot.y += headRot.y>180 ? -360 : headRot.y<-180 ? 360 :0;
                    headRot.x += headRot.x>180 ? -360 : headRot.x<-180 ? 360 :0;
                    headRot.x = Mathf.Clamp(headRot.x,-0.5f*verticalRotationRange,0.5f*verticalRotationRange);
                    quatHeadRot = Quaternion.Euler(headRot);
                    if(doingCamInterp){}
                }
            }break;
        }
    }
    public void ChangePerspective(PerspectiveModes newPerspective = PerspectiveModes._1stPerson){
        switch(newPerspective){
            case PerspectiveModes._1stPerson:{
                StopCoroutine("SmoothRot");
                isInThirdPerson = false;
                isInFirstPerson = true;
                transform.eulerAngles = Vector3.up* headRot.y;
                playerCamera.transform.localPosition = Vector3.up*eyeHeight;
                playerCamera.transform.localEulerAngles = (Vector2)playerCamera.transform.localEulerAngles;
                cameraPerspective = newPerspective;
                if(crosshairImg && autoGenerateCrosshair){
                    crosshairImg.gameObject.SetActive(true);
                }
            }break;

            case PerspectiveModes._3rdPerson:{
                StopCoroutine("SmoothRot");
                isInThirdPerson = true;
                isInFirstPerson = false;
                playerCamera.fieldOfView = initialCameraFOV;
                maxCameraDistInternal = maxCameraDistInternal == 0 ? capsule.radius*2 : maxCameraDistInternal;
                currentCameraZ = -(maxCameraDistInternal*0.85f);
                playerCamera.transform.localEulerAngles = (Vector2)playerCamera.transform.localEulerAngles;
                headRot.y = transform.eulerAngles.y;
                headRot.x = playerCamera.transform.eulerAngles.x;
                cameraPerspective = newPerspective;
                if(crosshairImg && autoGenerateCrosshair){
                    if(!showCrosshairIn3rdPerson){
                        crosshairImg.gameObject.SetActive(false);
                    }else{
                        crosshairImg.gameObject.SetActive(true);
                    }
                }
            }break;
        }
    }
    void FOVKick(){
        if(cameraPerspective == PerspectiveModes._1stPerson && FOVKickAmount>0){
            currentFOVMod = (!isIdle && isSprinting) ? initialCameraFOV+(FOVKickAmount*((sprintingSpeed/walkingSpeed)-1)) : initialCameraFOV;
            if(!Mathf.Approximately(playerCamera.fieldOfView, currentFOVMod) && playerCamera.fieldOfView >= initialCameraFOV){
                playerCamera.fieldOfView = Mathf.SmoothDamp(playerCamera.fieldOfView, currentFOVMod,ref FOVKickVelRef, Time.deltaTime,50);
            }
        }
    }
    void HeadbobCycleCalculator(){
        if(enableHeadbob){
            if(!isIdle && currentGroundInfo.isGettingGroundInfo && !isSliding){
                headbobWarmUp = Mathf.MoveTowards(headbobWarmUp, 1,Time.deltaTime*5);
                headbobCyclePosition += (_2DVelocity.magnitude)*(Time.deltaTime * (headbobSpeed/10));

                headbobCameraPosition.x = (((Mathf.Sin(Mathf.PI * (2*headbobCyclePosition + 0.5f)))*(headbobPower/50)))*headbobWarmUp;
                headbobCameraPosition.y = ((Mathf.Abs((((Mathf.Sin(Mathf.PI * (2*headbobCyclePosition)))*0.75f))*(headbobPower/50)))*headbobWarmUp )+eyeHeight;
                headbobCameraPosition.z = ((Mathf.Sin(Mathf.PI * (2*headbobCyclePosition))) * (ZTilt/3))*headbobWarmUp;
            }else{
                headbobCameraPosition = Vector3.MoveTowards(headbobCameraPosition,Vector3.up*eyeHeight,Time.deltaTime/(headbobPower*0.3f ));
                headbobWarmUp = 0.1f;
            }
            playerCamera.transform.localPosition = (Vector2)headbobCameraPosition;
            if(StepCycle>(headbobCyclePosition*3)){StepCycle = headbobCyclePosition+0.5f;}
        }
    }
    void UpdateCameraPosition_3rdPerson(){
       
        //if is moving, rotate capsule to match camera forward   //change buttondown to bool of isFiring or isTargeting
        if(!isIdle && !isSliding && currentGroundInfo.isGettingGroundInfo){
            Debug.DrawRay(transform.position,InputDir*10,Color.black);
            transform.rotation = Quaternion.Euler(0,Mathf.MoveTowardsAngle(transform.eulerAngles.y,(Mathf.Atan2(InputDir.x,InputDir.z)*Mathf.Rad2Deg),10), 0);
            //gameObject.transform.rotation = Quaternion.Euler(0,Mathf.MoveTowardsAngle(transform.eulerAngles.y, (Mathf.Atan2(InputDir.x,InputDir.z)*Mathf.Rad2Deg),20f), 0);
        }else if(isSliding){
            if(cameraPerspective==PerspectiveModes._3rdPerson){transform.eulerAngles = Vector3.up*Mathf.MoveTowardsAngle(transform.eulerAngles.y,(Mathf.Atan2(p_Rigidbody.velocity.x,p_Rigidbody.velocity.z)*Mathf.Rad2Deg),10);}
        }else if(!currentGroundInfo.isGettingGroundInfo && rotateCharaterToCameraForward){
            if(cameraPerspective==PerspectiveModes._3rdPerson){transform.eulerAngles = Vector3.up*Mathf.MoveTowardsAngle(transform.eulerAngles.y, headRot.y,10);}
        }

        currentCameraPos = headPos + (quatHeadRot *(Vector3.forward*currentCameraZ));
        playerCamera.transform.position = currentCameraPos;
        playerCamera.transform.LookAt(headPos);
       
    }
    #endregion

    #region Movement Functions
    void MovePlayer(Vector3 Direction, float Speed){
       // GroundInfo gI = GetGroundInfo();
        isIdle = Direction.normalized.magnitude <=0;
        _2DVelocity = Vector2.right * p_Rigidbody.velocity.x + Vector2.up * p_Rigidbody.velocity.z;
        speedToVelocityRatio = (Mathf.Lerp(0, 2, Mathf.InverseLerp(0, (sprintingSpeed/50), _2DVelocity.magnitude)));
        _2DVelocityMag = Mathf.Clamp((walkingSpeed/50) / _2DVelocity.magnitude, 0f,2f);
        
        if((currentGroundInfo.isGettingGroundInfo|| currentGroundInfo.potentialStair) && !Jumped && !isSliding && !doingPosInterp)
        {
            //Deceleration
            if(Direction.magnitude==0&& p_Rigidbody.velocity.normalized.magnitude>0.1f){
                p_Rigidbody.AddForce(-new Vector3(p_Rigidbody.velocity.x,currentGroundInfo.isInContactWithGround? p_Rigidbody.velocity.y-  Physics.gravity.y:0,p_Rigidbody.velocity.z)*(decelerationSpeed*Time.fixedDeltaTime),ForceMode.Force); 
            }
            //normal speed
            else if(currentGroundInfo.isInContactWithGround && currentGroundInfo.groundAngle<hardSlopeLimit){
                p_Rigidbody.velocity =  (Vector3.MoveTowards(p_Rigidbody.velocity,Vector3.ClampMagnitude(((Direction+(Vector3.up*(currentGroundInfo.groundInfluenceDirection.y*slopeInfluenceOnSpeed)))*((currentGroundInfo.potentialStair?550 :Speed)*Time.fixedDeltaTime))+(Vector3.down),Speed/50),1));
                //if(p_Rigidbody.position.y>(gI.playerGroundPosition+0.001f)&& p_Rigidbody.velocity.y<= 0){p_Rigidbody.AddForce(Physics.gravity,ForceMode.Impulse);print("Pushing down");}
                if(currentGroundInfo.potentialStair){
                    //p_Rigidbody.MovePosition((Vector3.right*p_Rigidbody.position.x)+(Vector3.up * currentGroundInfo.playerGroundPosition) + (Vector3.forward*p_Rigidbody.position.z));
                p_Rigidbody.AddForce(Vector3.up,ForceMode.Impulse);
                    //p_Rigidbody.AddForce(Vector3.up*50,ForceMode.Force);
                }
            }
            capsule.sharedMaterial = InputDir.magnitude>0 ? _ZeroFriction : _MaxFriction;
        }else if(isSliding){
            p_Rigidbody.AddForce(-(p_Rigidbody.velocity-Physics.gravity)*(slidingDeceleration*Time.fixedDeltaTime),ForceMode.Force);
        }else if(!currentGroundInfo.isGettingGroundInfo){
            //Air Control
            p_Rigidbody.AddForce((((Direction*(walkingSpeed))*Time.fixedDeltaTime)*airControlFactor*5)*currentGroundInfo.groundAngleMultiplier_Inverse_persistent,ForceMode.Acceleration);
            p_Rigidbody.velocity= Vector3.ClampMagnitude((Vector3.right*p_Rigidbody.velocity.x + Vector3.forward*p_Rigidbody.velocity.z) ,(walkingSpeed/50))+(Vector3.up*p_Rigidbody.velocity.y);
            
        }

        
    }
    void Jump(float Force){
        if((currentGroundInfo.isInContactWithGround) && 
            (currentGroundInfo.groundAngle<hardSlopeLimit) && 
            ((enableStaminaSystem && jumpingDepletesStamina)? currentStaminaLevel>s_JumpStaminaDepletion*1.2f : true) && 
            (Time.time>(jumpBlankingPeriod+0.1f)) &&
            (currentStance == Stances.Standing && !Jumped)){

                Jumped = true;
                p_Rigidbody.velocity =(Vector3.right * p_Rigidbody.velocity.x) + (Vector3.forward * p_Rigidbody.velocity.z);
                p_Rigidbody.AddForce(Vector3.up*(Force/10),ForceMode.Impulse);
                if(enableStaminaSystem && jumpingDepletesStamina){
                    InstantStaminaReduction(s_JumpStaminaDepletion);
                }
                capsule.sharedMaterial  = _ZeroFriction;
                jumpBlankingPeriod = Time.time;
        }
    }
    void Slide(){
        if(!isSliding){
            if(currentGroundInfo.isInContactWithGround){
                //do debug print
                if(enableMovementDebugging) {print("Starting Slide.");}
                p_Rigidbody.AddForce((transform.forward*((sprintingSpeed))+(Vector3.up*currentGroundInfo.groundInfluenceDirection.y)),ForceMode.Force);
                cachedDirPreSlide = transform.forward;
                cachedPosPreSlide = transform.position;
                capsule.sharedMaterial = _ZeroFriction;
                StartCoroutine(ApplyStance(slidingTransisionSpeed,Stances.Crouching));
                isSliding = true;
            }
        }else if(crouchInput){
            if(enableMovementDebugging) {print("Continuing Slide.");}
            if(Vector3.Distance(transform.position, cachedPosPreSlide)<maxFlatSlideDistance){p_Rigidbody.AddForce(cachedDirPreSlide*(sprintingSpeed/50),ForceMode.Force);}
            if(p_Rigidbody.velocity.magnitude>sprintingSpeed/50){p_Rigidbody.velocity= p_Rigidbody.velocity.normalized*(sprintingSpeed/50);}
            else if(p_Rigidbody.velocity.magnitude<(crouchingSpeed/25)){
                if(enableMovementDebugging) {print("Slide too slow, ending slide into crouch.");}
                //capsule.sharedMaterial = _MaxFrix;
                isSliding = false;
                isSprinting = false;
                StartCoroutine(ApplyStance(stanceTransisionSpeed,Stances.Crouching));
                currentGroundMovementSpeed = GroundSpeedProfiles.Crouching;
            }
        }else{
            if(OverheadCheck()){
                if(p_Rigidbody.velocity.magnitude>(walkingSpeed/50)){
                    if(enableMovementDebugging) {print("Key realeased, ending slide into a sprint.");}
                    isSliding = false;
                    StartCoroutine(ApplyStance(stanceTransisionSpeed,Stances.Standing));
                    currentGroundMovementSpeed = GroundSpeedProfiles.Sprinting;
                }else{
                     if(enableMovementDebugging) {print("Key realeased, ending slide into a walk.");}
                    isSliding = false;
                    StartCoroutine(ApplyStance(stanceTransisionSpeed,Stances.Standing));
                    currentGroundMovementSpeed = GroundSpeedProfiles.Walking;
                }
            }else{
                if(enableMovementDebugging) {print("Key realeased but there is an obstruction. Ending slide into crouch.");}
                isSliding = false;
                isSprinting = false;
                StartCoroutine(ApplyStance(stanceTransisionSpeed,Stances.Crouching));
                currentGroundMovementSpeed = GroundSpeedProfiles.Crouching;
            }

        }
    }
    void GetGroundInfo(){
        //to Get if we're actually touching ground.
        //to act as a normal and point buffer.
        currentGroundInfo.groundFromSweep = null;

        currentGroundInfo.groundFromSweep = Physics.SphereCastAll(transform.position,capsule.radius-0.001f,Vector3.down,((capsule.height/2))-(capsule.radius/2),whatIsGround);
    
        currentGroundInfo.isInContactWithGround = Physics.Raycast(transform.position, Vector3.down, out currentGroundInfo.groundFromRay, (capsule.height/2)+0.25f,whatIsGround);
        
        if(Jumped && (Physics.Raycast(transform.position, Vector3.down, (capsule.height/2)+0.1f,whatIsGround)||Physics.CheckSphere(transform.position-(Vector3.up*((capsule.height/2)-(capsule.radius-0.05f))),capsule.radius,whatIsGround)) &&Time.time>(jumpBlankingPeriod+0.1f)){Jumped=false;}
        
        //if(Result.isGrounded){
            if(currentGroundInfo.groundFromSweep!=null&&currentGroundInfo.groundFromSweep.Length!=0){
                currentGroundInfo.isGettingGroundInfo=true;
                //currentGroundInfo.groundNormal = averages.normalized/currentGroundInfo.groundFromSweep.Length;
                currentGroundInfo.groundNormal = (Vector3.right*currentGroundInfo.groundFromSweep.Average(x=> (x.point.y > currentGroundInfo.groundFromRay.point.y && Vector3.Angle(x.normal,Vector3.up)<hardSlopeLimit) ? x.normal.x : 0)) + (Vector3.up * currentGroundInfo.groundFromSweep.Average(x=> (x.point.y > currentGroundInfo.groundFromRay.point.y && Vector3.Angle(x.normal,Vector3.up)<hardSlopeLimit) ? x.normal.y :  1)) + (Vector3.forward * currentGroundInfo.groundFromSweep.Average(x=> (x.point.y > currentGroundInfo.groundFromRay.point.y && Vector3.Angle(x.normal,Vector3.up)<hardSlopeLimit) ? x.normal.z :  0));
                //currentGroundInfo.groundNormal = new Vector3(currentGroundInfo.groundFromSweep.Average(x=> x.normal.x), currentGroundInfo.groundFromSweep.Average(x=>x.normal.y), currentGroundInfo.groundFromSweep.Average(x=> x.normal.z));
                currentGroundInfo.groundRawYPosition = currentGroundInfo.groundFromSweep.Average(x=> (x.point.y > currentGroundInfo.groundFromRay.point.y && Vector3.Angle(x.normal,Vector3.up)<hardSlopeLimit) ? x.point.y :  currentGroundInfo.groundFromRay.point.y); //Mathf.MoveTowards(currentGroundInfo.groundRawYPosition, currentGroundInfo.groundFromSweep.Average(x=> (x.point.y > currentGroundInfo.groundFromRay.point.y && Vector3.Dot(x.normal,Vector3.up)<-0.25f) ? x.point.y :  currentGroundInfo.groundFromRay.point.y),Time.deltaTime*2);
                
            }else{
                currentGroundInfo.isGettingGroundInfo=false;
                currentGroundInfo.groundNormal = currentGroundInfo.groundFromRay.normal;
                currentGroundInfo.groundRawYPosition = currentGroundInfo.groundFromRay.point.y;
            }

            if(currentGroundInfo.isGettingGroundInfo){currentGroundInfo.groundAngleMultiplier_Inverse_persistent = currentGroundInfo.groundAngleMultiplier_Inverse;}
            //{
                currentGroundInfo.groundInfluenceDirection = Vector3.MoveTowards(currentGroundInfo.groundInfluenceDirection, Vector3.Cross(currentGroundInfo.groundNormal, Vector3.Cross(currentGroundInfo.groundNormal, Vector3.up)).normalized,2*Time.fixedDeltaTime);
                currentGroundInfo.groundAngle = Vector3.Angle(currentGroundInfo.groundNormal,Vector3.up);
                currentGroundInfo.groundAngleMultiplier_Inverse = ((currentGroundInfo.groundAngle-90)*-1)/90;
                currentGroundInfo.groundAngleMultiplier = ((currentGroundInfo.groundAngle))/90;
           //
            currentGroundInfo.groundTag = currentGroundInfo.isInContactWithGround ? currentGroundInfo.groundFromRay.transform.tag : string.Empty;
            currentGroundInfo.potentialStair = 
                /* (Vector3.Dot(InputDir,currentGroundInfo.groundInfluenceDirection)<0)&& */
                /* (currentGroundInfo.groundRawYPosition>currentGroundInfo.groundFromRay.point.y+0.01f)&& */
                (Physics.Raycast((Vector3.right*transform.position.x+(Vector3.up*(currentGroundInfo.groundRawYPosition+0.02f))+Vector3.forward*transform.position.z),(InputDir+(Vector3.up*0.5f))*capsule.radius,out currentGroundInfo.stairCheck,capsule.radius*2,whatIsGround)||
                Physics.Raycast((Vector3.right*transform.position.x+(Vector3.up*(currentGroundInfo.groundRawYPosition+0.02f))+Vector3.forward*transform.position.z),(InputDir+(Vector3.up*-0.25f))*capsule.radius,out currentGroundInfo.stairCheck,capsule.radius*2,whatIsGround))&& 
                Vector3.Angle(currentGroundInfo.stairCheck.normal,Vector3.up)>hardSlopeLimit&&
                //!Physics.Raycast((Vector3.right*transform.position.x+(Vector3.up*(currentGroundInfo.groundRawYPosition+maxStairRise))+Vector3.forward*transform.position.z),(InputDir),capsule.radius+minimumTreadDepth,whatIsGround)&& 
                Physics.Raycast(currentGroundInfo.stairCheck.point+(transform.forward*0.01f)+(Vector3.up*maxStairRise),Vector3.down,out currentGroundInfo.stairCheck,maxStairRise*1.1f,whatIsGround)&&
                Vector3.Angle(currentGroundInfo.stairCheck.normal,Vector3.up)<10;
            currentGroundInfo.playerGroundPosition = Mathf.MoveTowards(currentGroundInfo.playerGroundPosition, currentGroundInfo.groundRawYPosition+ (capsule.height/2) + 0.01f,0.05f);
        //}

        if(currentGroundInfo.isInContactWithGround && enableFootstepSounds && shouldCalculateFootstepTriggers){
            if(currentGroundInfo.groundFromRay.collider as TerrainCollider){
                currentGroundInfo.groundMaterial = null;
                currentGroundInfo.currentTerrain = currentGroundInfo.groundFromRay.transform.GetComponent<Terrain>();
                if(currentGroundInfo.currentTerrain){
                    Vector2 XZ = (Vector2.right* (((transform.position.x - currentGroundInfo.currentTerrain.transform.position.x)/currentGroundInfo.currentTerrain.terrainData.size.x)) * currentGroundInfo.currentTerrain.terrainData.alphamapWidth) + (Vector2.up* (((transform.position.z - currentGroundInfo.currentTerrain.transform.position.z)/currentGroundInfo.currentTerrain.terrainData.size.z)) * currentGroundInfo.currentTerrain.terrainData.alphamapHeight);
                    float[,,] aMap = currentGroundInfo.currentTerrain.terrainData.GetAlphamaps((int)XZ.x, (int)XZ.y, 1, 1);
                    for(int i =0; i < aMap.Length; i++){
                        if(aMap[0,0,i]==1 ){
                            //print(currentGroundInfo.currentTerrain.terrainData.terrainLayers[i].name);
                            currentGroundInfo.groundLayer = currentGroundInfo.currentTerrain.terrainData.terrainLayers[i];
                            break;
                        }
                    }
                }else{currentGroundInfo.groundLayer = null;}                
            }else{
                currentGroundInfo.groundLayer = null;
                currentGroundInfo.currentMesh = currentGroundInfo.groundFromRay.transform.GetComponent<MeshFilter>().sharedMesh;
                if(currentGroundInfo.currentMesh){
                    int limit = currentGroundInfo.groundFromRay.triangleIndex*3, submesh;
                    for(submesh = 0; submesh<currentGroundInfo.currentMesh.subMeshCount; submesh++){
                        int indices = currentGroundInfo.currentMesh.GetTriangles(submesh).Length;
                        if(indices>limit){break;}
                        limit -= indices;
                    }
                    currentGroundInfo.groundMaterial = currentGroundInfo.groundFromRay.transform.GetComponent<Renderer>().sharedMaterials[submesh];
                }else{currentGroundInfo.groundMaterial = null; }
            }
        }else{currentGroundInfo.groundMaterial = null; currentGroundInfo.groundLayer = null;}
        #if UNITY_EDITOR
        if(enableGroundingDebugging){
        print("Grounded: "+currentGroundInfo.isInContactWithGround + ", Ground Hits: "+ currentGroundInfo.groundFromSweep.Length +", Ground Angle: "+currentGroundInfo.groundAngle.ToString("0.00") + ", Ground Multi: "+ currentGroundInfo.groundAngleMultiplier.ToString("0.00") + ", Ground Multi Inverse: "+ currentGroundInfo.groundAngleMultiplier_Inverse.ToString("0.00"));
        Debug.DrawRay(transform.position, Vector3.down*((capsule.height/2)+0.1f),Color.green);
        Debug.DrawRay(transform.position, currentGroundInfo.groundInfluenceDirection,Color.magenta);
    

        }
        #endif
    }
    void GroundMovementSpeedUpdate(){
        if(!isVaulting){
            switch (currentGroundMovementSpeed){
                case GroundSpeedProfiles.Walking:{
                    if(isCrouching || isSprinting){
                        isSprinting = false;
                        isCrouching = false;
                        currentGroundSpeed = walkingSpeed;
                        StopCoroutine("ApplyStance");
                        StartCoroutine(ApplyStance(stanceTransisionSpeed,Stances.Standing));
                    }
                    if(vaultInput && canVault){VaultCheck();}
                    //check for state change call
                    if((canCrouch&&crouchInputDown)||crouchOverride){
                        isCrouching = true;
                        isSprinting = false;
                        currentGroundSpeed = crouchingSpeed;
                        currentGroundMovementSpeed = GroundSpeedProfiles.Crouching;
                        StopCoroutine("ApplyStance");
                        StartCoroutine(ApplyStance(stanceTransisionSpeed,Stances.Crouching));
                        break;
                    }else if((canSprint&&sprintInputDown && ((enableStaminaSystem && jumpingDepletesStamina)? currentStaminaLevel>S_minimumStaminaToSprint : true) && (enableSurvivalStats ? (!currentSurvivalStats.isDehydrated && !currentSurvivalStats.isStarving) : true))||sprintOveride){
                        isCrouching = false;
                        isSprinting = true;
                        currentGroundSpeed = sprintingSpeed;
                        currentGroundMovementSpeed = GroundSpeedProfiles.Sprinting;
                        StopCoroutine("ApplyStance");
                        StartCoroutine(ApplyStance(stanceTransisionSpeed,Stances.Standing));
                    }
                    break;
                }
                
                case GroundSpeedProfiles.Crouching:{
                    if(!isCrouching){
                        isCrouching = true;
                        isSprinting = false;
                        currentGroundSpeed = crouchingSpeed;
                        StopCoroutine("ApplyStance");
                        StartCoroutine(ApplyStance(stanceTransisionSpeed,Stances.Crouching));
                    }


                    //check for state change call
                    if((toggleCrouch ? crouchInputDown : !crouchInput)&&!crouchOverride && OverheadCheck()){
                        isCrouching = false;
                        isSprinting = false;
                        currentGroundSpeed = walkingSpeed;
                        currentGroundMovementSpeed = GroundSpeedProfiles.Walking;
                        StopCoroutine("ApplyStance");
                        StartCoroutine(ApplyStance(stanceTransisionSpeed,Stances.Standing));
                        break;
                    }else if(((canSprint && sprintInputDown && ((enableStaminaSystem && jumpingDepletesStamina)? currentStaminaLevel>S_minimumStaminaToSprint : true)&&(enableSurvivalStats ? (!currentSurvivalStats.isDehydrated && !currentSurvivalStats.isStarving) : true))||sprintOveride) && OverheadCheck()){
                        isCrouching = false;
                        isSprinting = true;
                        currentGroundSpeed = sprintingSpeed;
                        currentGroundMovementSpeed = GroundSpeedProfiles.Sprinting;
                        StopCoroutine("ApplyStance");
                        StartCoroutine(ApplyStance(stanceTransisionSpeed,Stances.Standing));
                    }
                    break;
                }

                case GroundSpeedProfiles.Sprinting:{
                    //if(!isIdle)
                    {
                        if(!isSprinting){
                            isCrouching = false;
                            isSprinting = true;
                            currentGroundSpeed = sprintingSpeed;
                            StopCoroutine("ApplyStance");
                            StartCoroutine(ApplyStance(stanceTransisionSpeed,Stances.Standing));
                        } 
                        
                        if((vaultInput || autoVaultWhenSpringing) && canVault){VaultCheck();}
                        
                        //check for state change call
                        if(canSlide && !isIdle && crouchInputDown && currentGroundInfo.isInContactWithGround){
                            Slide();
                            currentGroundMovementSpeed = GroundSpeedProfiles.Sliding;
                            break;
                        }


                        else if((canCrouch&& crouchInputDown)||crouchOverride){
                            isCrouching = true;
                            isSprinting = false;
                            currentGroundSpeed = crouchingSpeed;
                            currentGroundMovementSpeed = GroundSpeedProfiles.Crouching;
                            StopCoroutine("ApplyStance");
                            StartCoroutine(ApplyStance(stanceTransisionSpeed,Stances.Crouching));
                            break;
                            //Can't leave sprint in toggle sprint.
                        }else if((toggleSprint ? sprintInputDown : !sprintInput)&&!sprintOveride){
                            isCrouching = false;
                            isSprinting = false;
                            currentGroundSpeed = walkingSpeed;
                            currentGroundMovementSpeed = GroundSpeedProfiles.Walking;
                            StopCoroutine("ApplyStance");
                            StartCoroutine(ApplyStance(stanceTransisionSpeed,Stances.Standing));
                        }
                        break;
                    }
                }
                case GroundSpeedProfiles.Sliding:{
                }break;
            }
        }
    }
    IEnumerator ApplyStance(float smoothSpeed, Stances newStance){
        currentStance = newStance;
        while(!Mathf.Approximately(capsule.height,currentStance==Stances.Standing? standingHeight : crouchingHeight)){
        
            capsule.height = (smoothSpeed>0? Mathf.MoveTowards(capsule.height, currentStance==Stances.Standing? standingHeight : crouchingHeight, stanceTransisionSpeed*Time.fixedDeltaTime) :  currentStance==Stances.Standing? standingHeight : crouchingHeight);
            //may need to do this differently.
            if(currentStance == Stances.Crouching && currentGroundInfo.isGettingGroundInfo){
                p_Rigidbody.velocity = p_Rigidbody.velocity+(Vector3.down*2);
                if(enableMovementDebugging) {print("Applying Stance and applying down force ");}
             //   p_Rigidbody.MovePosition((Vector3.right*p_Rigidbody.position.x) +(Vector3.up*(Mathf.MoveTowards(p_Rigidbody.position.y, currentGroundInfo.playerGroundPosition,stanceTransisionSpeed*Time.fixedDeltaTime)))+ (Vector3.forward*p_Rigidbody.position.z));
            }
            yield return new WaitForFixedUpdate();
        }
    }
    bool OverheadCheck(){    //Returns true when there is no obstruction.
        bool result = false;
        if(Physics.Raycast(transform.position,Vector3.up,standingHeight - (capsule.height/2),whatIsGround)){result = true;}
        return !result;
    }
    #endregion

    #region Stamina System
    private void CalculateStamina(){
        if(isSprinting && !ignoreStamina && !isIdle){
            if(currentStaminaLevel!=0){
                currentStaminaLevel = Mathf.MoveTowards(currentStaminaLevel, 0, s_depletionSpeed*Time.deltaTime);
            }else if(!isSliding){ currentGroundMovementSpeed = GroundSpeedProfiles.Walking;}
        }
        else if(currentStaminaLevel != Stamina && !ignoreStamina && (enableSurvivalStats ? (!currentSurvivalStats.isDehydrated && !currentSurvivalStats.isStarving) : true)){
            currentStaminaLevel = Mathf.MoveTowards(currentStaminaLevel, Stamina, s_regenerationSpeed*Time.deltaTime);
        }
    }
    public void InstantStaminaReduction(float Reduction){
        if(!ignoreStamina && enableStaminaSystem){currentStaminaLevel = Mathf.Clamp(currentStaminaLevel-=Reduction, 0, Stamina);}
    }
    #endregion

    #region Footstep System
    void CalculateFootstepTriggers(){
        if(enableFootstepSounds&& footstepTriggeringMode == FootstepTriggeringMode.calculatedTiming && shouldCalculateFootstepTriggers){
            if(_2DVelocity.magnitude>(currentGroundSpeed/100)&& !isIdle){
                if(cameraPerspective == PerspectiveModes._1stPerson && enableHeadbob){
                    if((enableHeadbob ? headbobCyclePosition : Time.time) > StepCycle && currentGroundInfo.isGettingGroundInfo && !isSliding){
                        //print("Steped");
                        CallFootstepClip();
                        StepCycle = enableHeadbob ? (headbobCyclePosition+0.5f) : (Time.time+(stepTiming*_2DVelocityMag));
                    }
                }else{
                    if(Time.time > StepCycle && currentGroundInfo.isGettingGroundInfo && !isSliding){
                        //print("Steped");
                        CallFootstepClip();
                        StepCycle = (Time.time+(stepTiming*(_2DVelocityMag)));
                    }
                }
            }
        }
    }
    public void CallFootstepClip(){
        if(playerAudioSource){
            if(enableFootstepSounds && footstepSoundSet.Any()){
                for(int i = 0; i< footstepSoundSet.Count(); i++){
                    if(footstepSoundSet[i].profileTriggerType == MatProfileType.Material ? footstepSoundSet[i]._Material == currentGroundInfo.groundMaterial : footstepSoundSet[i]._Layer == currentGroundInfo.groundLayer){
                        currentClipSet = footstepSoundSet[i].footstepClips;
                        break;
                    }else if(i == footstepSoundSet.Count-1){
                        currentClipSet = null;  
                    }
                }
                if(currentClipSet!=null){
                    playerAudioSource.PlayOneShot(currentClipSet[Random.Range(0,currentClipSet.Count())]);
                }
            }
        }
    }
    #endregion

    #region Parkour Functions
    void VaultCheck(){
        if(!isVaulting){
            if(enableVaultDebugging){ Debug.DrawRay(transform.position-(Vector3.up*(capsule.height/4)), transform.forward*(capsule.radius*2), Color.blue,120);}
            if(Physics.Raycast(transform.position-(Vector3.up*(capsule.height/4)), transform.forward,out VC_Stage1,capsule.radius*2) && VC_Stage1.transform.CompareTag(vaultObjectTag)){
                float vaultObjAngle = Mathf.Acos(Vector3.Dot(Vector3.up,(Quaternion.LookRotation(VC_Stage1.normal,Vector3.up)*Vector3.up))) * Mathf.Rad2Deg;

                if(enableVaultDebugging) {Debug.DrawRay((VC_Stage1.normal*-0.05f)+(VC_Stage1.point+((Quaternion.LookRotation(VC_Stage1.normal,Vector3.up)*Vector3.up)*(maxVaultHeight))), -(Quaternion.LookRotation(VC_Stage1.normal,Vector3.up)*Vector3.up)*(capsule.height),Color.cyan,120);}
                if(Physics.Raycast((VC_Stage1.normal*-0.05f)+(VC_Stage1.point+((Quaternion.LookRotation(VC_Stage1.normal,Vector3.up)*Vector3.up)*(maxVaultHeight))), -(Quaternion.LookRotation(VC_Stage1.normal,Vector3.up)*Vector3.up), out VC_Stage2,capsule.height) && VC_Stage2.transform == VC_Stage1.transform && VC_Stage2.point.y <= currentGroundInfo.groundRawYPosition+maxVaultHeight+vaultObjAngle){
                    vaultForwardVec = -VC_Stage1.normal;

                    if(enableVaultDebugging) {Debug.DrawLine(VC_Stage2.point+(vaultForwardVec*maxVaultDepth)-(Vector3.up*0.01f), (VC_Stage2.point- (Vector3.up*.01f)), Color.red,120   );}
                    if(Physics.Linecast((VC_Stage2.point+(vaultForwardVec*maxVaultDepth))-(Vector3.up*0.01f), VC_Stage2.point - (Vector3.up*0.01f),out VC_Stage3)){
                        Ray vc4 = new Ray(VC_Stage3.point+(vaultForwardVec*(capsule.radius+(vaultObjAngle*0.01f))),Vector3.down);
                        if(enableVaultDebugging){ Debug.DrawRay(vc4.origin, vc4.direction,Color.green,120);}
                        Physics.SphereCast(vc4,capsule.radius,out VC_Stage4,maxVaultHeight+(capsule.height/2));
                        Vector3 proposedPos = ((Vector3.right*vc4.origin.x)+(Vector3.up*(VC_Stage4.point.y+(capsule.height/2)+0.01f))+(Vector3.forward*vc4.origin.z)) + (VC_Stage3.normal*0.02f);

                        if(VC_Stage4.collider && !Physics.CheckCapsule(proposedPos-(Vector3.up*((capsule.height/2)-capsule.radius)), proposedPos+(Vector3.up*((capsule.height/2)-capsule.radius)),capsule.radius)){
                            isVaulting = true;
                            StopCoroutine("PositionInterp");
                            StartCoroutine(PositionInterp(proposedPos, vaultSpeed));

                        }else if(enableVaultDebugging){Debug.Log("Cannot Vault this Object. Sufficient space/ground was not found on the other side of the vault object.");}
                    }else if(enableVaultDebugging){Debug.Log("Cannot Vault this object. Object is too deep or there is an obstruction on the other side.");}
                }if(enableVaultDebugging){Debug.Log("Vault Object is too high or there is something ontop of the object that is not marked as vaultable.");}

            }

        }else if(!doingPosInterp){
            isVaulting = false;
        }
    }
    
    IEnumerator PositionInterp(Vector3 pos, float speed){
        doingPosInterp = true;
        Vector3 vel = p_Rigidbody.velocity;
        p_Rigidbody.useGravity = false;
        p_Rigidbody.velocity = Vector3.zero;
        capsule.enabled = false;
        while(Vector3.Distance(p_Rigidbody.position, pos)>0.01f){
            p_Rigidbody.velocity = Vector3.zero;
            p_Rigidbody.position = (Vector3.MoveTowards(p_Rigidbody.position, pos,speed*Time.fixedDeltaTime));
            yield return new WaitForFixedUpdate();
        }
        capsule.enabled = true;
        p_Rigidbody.useGravity = true;
        p_Rigidbody.velocity = vel;
        doingPosInterp = false;
        if(isVaulting){VaultCheck();}
    }
    #endregion

    #region Survival Stat Functions
    public void TickStats(){
        if(currentSurvivalStats.Hunger>0){
            currentSurvivalStats.Hunger = Mathf.Clamp(currentSurvivalStats.Hunger-(hungerDepletionRate+(isSprinting&&!isIdle ? 0.1f:0)), 0, defaultSurvivalStats.Hunger);
            currentSurvivalStats.isStarving = (currentSurvivalStats.Hunger<(defaultSurvivalStats.Hunger/10));
        }
        if(currentSurvivalStats.Hydration>0){
            currentSurvivalStats.Hydration = Mathf.Clamp(currentSurvivalStats.Hydration-(hydrationDepletionRate+(isSprinting&&!isIdle ? 0.1f:0)), 0, defaultSurvivalStats.Hydration);
            currentSurvivalStats.isDehydrated = (currentSurvivalStats.Hydration<(defaultSurvivalStats.Hydration/8));
        }
        currentSurvivalStats.hasLowHealth = (currentSurvivalStats.Health<(defaultSurvivalStats.Health/10));

        StatTickTimer = Time.time + (60/statTickRate);
    }
    public void ImmediateStateChage(float Amount, StatSelector Stat = StatSelector.Health){
        switch (Stat){
            case StatSelector.Health:{
                currentSurvivalStats.Health = Mathf.Clamp(currentSurvivalStats.Health+Amount,0,defaultSurvivalStats.Health);
                currentSurvivalStats.hasLowHealth = (currentSurvivalStats.Health<(defaultSurvivalStats.Health/10));

            }break;

            case StatSelector.Hunger:{
                currentSurvivalStats.Hunger = Mathf.Clamp(currentSurvivalStats.Hunger+Amount,0,defaultSurvivalStats.Hunger);
                currentSurvivalStats.isStarving = (currentSurvivalStats.Hunger<(defaultSurvivalStats.Hunger/10));
            }break;

            case StatSelector.Hydration:{
                currentSurvivalStats.Hydration = Mathf.Clamp(currentSurvivalStats.Hydration+Amount,0,defaultSurvivalStats.Hydration);
                currentSurvivalStats.isDehydrated = (currentSurvivalStats.Hydration<(defaultSurvivalStats.Hydration/8));
            }break;
        }
    }
    public void LevelUpStat(float newMaxStatLevel, StatSelector Stat = StatSelector.Health, bool Refill = true){
        switch(Stat){
            case StatSelector.Health:{
                defaultSurvivalStats.Health = Mathf.Clamp(newMaxStatLevel,0,newMaxStatLevel);;
                if(Refill){currentSurvivalStats.Health = Mathf.Clamp(newMaxStatLevel,0,newMaxStatLevel);}
                currentSurvivalStats.hasLowHealth = (currentSurvivalStats.Health<(defaultSurvivalStats.Health/10));

            }break;
            case StatSelector.Hunger:{
                defaultSurvivalStats.Hunger = Mathf.Clamp(newMaxStatLevel,0,newMaxStatLevel);;
                if(Refill){currentSurvivalStats.Hunger = Mathf.Clamp(newMaxStatLevel,0,newMaxStatLevel);}
                currentSurvivalStats.isStarving = (currentSurvivalStats.Hunger<(defaultSurvivalStats.Hunger/10));

            }break;
            case StatSelector.Hydration:{
                defaultSurvivalStats.Hydration = Mathf.Clamp(newMaxStatLevel,0,newMaxStatLevel);;
                if(Refill){currentSurvivalStats.Hydration = Mathf.Clamp(newMaxStatLevel,0,newMaxStatLevel);}
                currentSurvivalStats.isDehydrated = (currentSurvivalStats.Hydration<(defaultSurvivalStats.Hydration/8));

            }break;
        }
    }
    
    #endregion

    #region Animator Update
    void UpdateAnimationTriggers(){
        switch (cameraPerspective){
            case PerspectiveModes._1stPerson:{
                if(_1stPersonCharacterAnimator){
                    //Setup Fistperson animation triggers here.

                }
            }break;
            
            case PerspectiveModes._3rdPerson:{
                if(_3rdPersonCharacterAnimator){
                    //Setup Thirdperson animation triggers here.
                    
                }

            }break;
        }
    }
    #endregion

    #region Gizmos
    #if UNITY_EDITOR
    private void OnDrawGizmos() {
        if(enableGroundingDebugging){
            if(Application.isPlaying){
               
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position-(Vector3.up*((capsule.height/2)-(capsule.radius+0.1f))),capsule.radius);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position-(Vector3.up*((capsule.height/2)-(capsule.radius-0.5f))),capsule.radius);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(new Vector3(transform.position.x,currentGroundInfo.playerGroundPosition,transform.position.z),0.05f);
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(new Vector3(transform.position.x,currentGroundInfo.groundRawYPosition,transform.position.z),0.05f);

            }
        
        }
    
        if(enableVaultDebugging &&Application.isPlaying){
            Gizmos.DrawWireSphere(VC_Stage3.point+(vaultForwardVec*(capsule.radius)),capsule.radius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(VC_Stage4.point,capsule.radius);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(((Vector3.right*(VC_Stage3.point+(vaultForwardVec*(capsule.radius))).x)+(Vector3.up*(VC_Stage4.point.y+(capsule.height/2)+0.01f))+(Vector3.forward*(VC_Stage3.point+(vaultForwardVec*(capsule.radius))).z)),capsule.radius);
        }
    }
    #endif
    #endregion

}


#region Classes and Enums
[System.Serializable]
public class GroundInfo{
    public bool isInContactWithGround, isGettingGroundInfo, potentialStair;
    public float groundAngleMultiplier_Inverse = 1, groundAngleMultiplier_Inverse_persistent = 1, groundAngleMultiplier = 0, groundAngle, playerGroundPosition, groundRawYPosition;
    public Vector3 groundInfluenceDirection, groundNormal;
    public string groundTag;
    public Material groundMaterial;
    public TerrainLayer groundLayer;
    internal Terrain currentTerrain;
    internal Mesh currentMesh;
    internal RaycastHit groundFromRay, stairCheck;
    internal RaycastHit[] groundFromSweep;

    
}
[System.Serializable]
public class GroundMaterialProfile{
    public MatProfileType profileTriggerType = MatProfileType.Material;
    public Material _Material;
    public TerrainLayer _Layer;
    public List<AudioClip> footstepClips = new List<AudioClip>();
}
[System.Serializable]
public class SurvivalStats{
    [Range(0.0f,500.0f)]public float Health = 250.0f, Hunger = 100.0f, Hydration = 100f;
    public bool hasLowHealth, isStarving, isDehydrated;
}
public enum StatSelector{Health, Hunger, Hydration}
public enum MatProfileType {Material, terrainLayer}
public enum FootstepTriggeringMode{calculatedTiming, calledFromAnimations}
public enum PerspectiveModes{_1stPerson, _3rdPerson}
public enum ViewInputModes{Traditional, Retro}
public enum MouseInputInversionModes{None, X, Y, Both}
public enum GroundSpeedProfiles{Crouching, Walking, Sprinting, Sliding}
public enum Stances{Standing, Crouching}
public enum MovementModes{Idle, Walking, Sprinting, Crouching, Sliding}
#endregion

#region Editor Scripting
#if UNITY_EDITOR
[CustomEditor(typeof(SUPERFirstPerson))]
public class SuperFPEditor : Editor{
    SUPERFirstPerson t;
    SerializedObject tSO, SurvivalStatsTSO;
    SerializedProperty obstructionMaskField, groundLayerMask, groundMatProf, defaultSurvivalStats, currentSurvivalStats;

    public void OnEnable(){
        t = (SUPERFirstPerson)target;
        tSO = new SerializedObject(t);
        SurvivalStatsTSO = new SerializedObject(t);
        obstructionMaskField = tSO.FindProperty("cameraObstructionIgnore");
        groundLayerMask = tSO.FindProperty("whatIsGround");
        groundMatProf = tSO.FindProperty("footstepSoundSet");
    }

    public override void OnInspectorGUI(){
        
        #region PlaymodeWarning
        if(Application.isPlaying){
            EditorGUILayout.HelpBox("It is recommended you switch to another Gameobject's inspector, Updates to this inspector panel during playmode can cause lag in the rigidbody calculations and cause unwanted adverse effects to gameplay. \n\n Please note this is NOT an issue in application builds.", MessageType.Warning);
        }
        #endregion

        #region Label  
        EditorGUILayout.Space();
        //GUILayout.Label("<b><i><size=16><color=#B2F9CF>S</color><color=#F9B2DC>U</color><color=#CFB2F9>P</color><color=#B2F9F3>E</color><color=#F9CFB2>R</color></size></i><size=12>First Person Controller</size></b>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,richText = true, fontSize = 16},GUILayout.ExpandWidth(true));
        
        //GUILayout.Label("<b><i><size=16><color=#3FB8AF>S</color><color=#7FC7AF>U</color><color=#DAD8A7>P</color><color=#FF9E9D>E</color><color=#FF3D7F>R</color></size></i><size=12>First Person Controller</size></b>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,richText = true, fontSize = 16},GUILayout.ExpandWidth(true));
        
        GUILayout.Label("<b><i><size=18><color=#FC80A5>S</color><color=#FFFF9F>U</color><color=#99FF99>P</color><color=#76D7EA>E</color><color=#BF8FCC>R</color></size></i></b> <size=12>First Person Controller</size>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,richText = true, fontSize = 16},GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("",GUI.skin.horizontalSlider,GUILayout.MaxHeight(6)); EditorGUILayout.Space();
        #endregion

        #region Camera Settings
        GUILayout.Label("Camera Settings",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 13},GUILayout.ExpandWidth(true));
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        t.playerCamera = (Camera)EditorGUILayout.ObjectField(new GUIContent("Player Camera", "The Camera Attached to the Player."),t.playerCamera,typeof(Camera),true);
        t.cameraPerspective = (PerspectiveModes)EditorGUILayout.EnumPopup(new GUIContent("Camera Perspective Mode", "The current perspective of the character."),t.cameraPerspective);
        if(t.cameraPerspective == PerspectiveModes._3rdPerson){EditorGUILayout.HelpBox("3rd Person perspective is currently very experimental. Bugs and other adverse effects may occur.",MessageType.Info);}
        t.automaticallySwitchPerspective = EditorGUILayout.ToggleLeft(new GUIContent("Automatically Switch Perspective", "Should the Camera perspective mode automatically change based on the distance between the camera and the character's head?"),t.automaticallySwitchPerspective);
        #if ENABLE_INPUT_SYSTEM
        #else
        if(!t.automaticallySwitchPerspective){t.perspectiveSwitchingKey = (KeyCode)EditorGUILayout.EnumPopup(new GUIContent("Perspective Switch Key", "The keyboard key used to switch perspective modes. Set to none if you do not wish to allow perspective switching"),t.perspectiveSwitchingKey);}
        #endif
        t.mouseInputInversion = (MouseInputInversionModes)EditorGUILayout.EnumPopup(new GUIContent("Mouse Input Inversion", "Which axes of the mouse input should be inverted if any?"),t.mouseInputInversion);
        t.Sensitivity = EditorGUILayout.Slider(new GUIContent("Mouse Sensitivity", "Sensitivity of the mouse"),t.Sensitivity,1,20);
        t.rotationWeight = EditorGUILayout.Slider(new GUIContent("Camera Weight", "How heavy should the camera feel?"),t.rotationWeight, 1,25);
        t.verticalRotationRange =EditorGUILayout.Slider(new GUIContent("Vertical Rotation Range", "The vertical angle range (In degrees) that the camera is allowed to move in"),t.verticalRotationRange,1,180);
        t.eyeHeight = EditorGUILayout.Slider(new GUIContent("Eye Height", "The Eye height of the player measured from the center of the character's capsule and upwards."),t.eyeHeight,0,1);
        t.lockAndHideMouse = EditorGUILayout.ToggleLeft(new GUIContent("Lock and Hide mouse Cursor", "Should the controller lock and hide the cursor?"),t.lockAndHideMouse);
        t.autoGenerateCrosshair = EditorGUILayout.ToggleLeft(new GUIContent("Auto Generate Crosshair", "Should the controller automatically generate a crosshair?"),t.autoGenerateCrosshair);
        GUI.enabled = t.autoGenerateCrosshair;
        t.showCrosshairIn3rdPerson = EditorGUILayout.ToggleLeft(new GUIContent("Show Crosshair in 3rd person?", "Should the controller show the crosshair in 3rd person?"),t.showCrosshairIn3rdPerson);
        t.crosshairSprite = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Crosshair Sprite", "The Sprite the controller will use when generating a crosshair."),t.crosshairSprite, typeof(Sprite),false, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        GUI.enabled = true;
        EditorGUILayout.Space(20);

        if(t.cameraPerspective == PerspectiveModes._1stPerson){
            t.viewInputMethods = (ViewInputModes)EditorGUILayout.EnumPopup(new GUIContent("Camera Input Methods", "The input method used to rotate the camera."),t.viewInputMethods);
            t.FOVKickAmount = EditorGUILayout.Slider(new GUIContent("FOV Kick Amount", "How much should the camera's FOV change based on the current movement speed?"),t.FOVKickAmount,0,50);
            t.FOVSensitivityMultiplier = EditorGUILayout.Slider(new GUIContent("FOV Sensitivity Multiplier", "How much should the camera's FOV effect the mouse sensitivity? (Lower FOV = less sensitive)"),t.FOVSensitivityMultiplier,0,1);
        }else{
            t.rotateCharaterToCameraForward = EditorGUILayout.ToggleLeft(new GUIContent("Rotate Ungrounded Chareter to Camera Forward", "Should the character get rotated towards the camera's forward facing direction when mid air?"),t.rotateCharaterToCameraForward);
            t.maxCameraDistance = EditorGUILayout.Slider(new GUIContent("Max Camera Distance", "The furthest distance the camera is allowed to hover from the character's head"),t.maxCameraDistance,0,15);
            t.cameraZoomSensitivity = EditorGUILayout.Slider(new GUIContent("Camera Zoom Sensitivity", "How sensitive should the mouse scroll wheel be when zooming the camera in and out?"),t.cameraZoomSensitivity, 1,5);
            EditorGUILayout.PropertyField(obstructionMaskField,new GUIContent("Camera Obstruction Layers", "The Layers the camera will registar as an obstruction and move in front of ."));
        }
        EditorGUILayout.EndVertical();
        if(GUI.changed){EditorUtility.SetDirty(t); Undo.RecordObject(t,"Undo Camera Setting changes"); tSO.ApplyModifiedProperties();}
        #endregion
    
        #region Movement Settings

        EditorGUILayout.Space(); EditorGUILayout.LabelField("",GUI.skin.horizontalSlider,GUILayout.MaxHeight(6)); EditorGUILayout.Space();
        GUILayout.Label("Movement Settings",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 13},GUILayout.ExpandWidth(true));
        EditorGUILayout.Space(20);

        #region Stances and Speed
        GUILayout.Label("<color=grey>Stances and Speed</color>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 10, richText = true},GUILayout.ExpandWidth(true));
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.Space(15);
        
        GUI.enabled = false;
        t.currentGroundMovementSpeed = (GroundSpeedProfiles)EditorGUILayout.EnumPopup(new GUIContent("Current Movement Speed", "Displays the player's current movement speed,"),t.currentGroundMovementSpeed);
        GUI.enabled = true;

        EditorGUILayout.Space();
        t.walkingSpeed = EditorGUILayout.Slider(new GUIContent("Walking Speed", "How quickly can the player move while walking?"),t.walkingSpeed,1,400);

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        t.canSprint = EditorGUILayout.ToggleLeft(new GUIContent("Can Sprint", "Is the player allowed to enter a sprint?"),t.canSprint);
        GUI.enabled = t.canSprint;
        t.toggleSprint = EditorGUILayout.ToggleLeft(new GUIContent("Toggle Sprint", "Should the spring key act as a toggle?"),t.toggleSprint);
        #if ENABLE_INPUT_SYSTEM
        #else
        t.sprintKey = (KeyCode)EditorGUILayout.EnumPopup(new GUIContent("Sprint Key", "The Key used to enter a sprint."),t.sprintKey);
        #endif
        t.sprintingSpeed = EditorGUILayout.Slider(new GUIContent("Sprinting Speed", "How quickly can the player move while sprinting?"),t.sprintingSpeed,t.walkingSpeed+1,650);
        t.decelerationSpeed = EditorGUILayout.Slider(new GUIContent("Deceleration Factor", "Behaves somewhat like a brakeing force"),t.decelerationSpeed,1,300);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        t.canCrouch = EditorGUILayout.ToggleLeft(new GUIContent("Can Crouch", "Is the player allowed to crouch?"), t.canCrouch);
        GUI.enabled = t.canCrouch;
        t.toggleCrouch = EditorGUILayout.ToggleLeft(new GUIContent("Toggle Crouch", "Should pressing the crouch button act as a toggle?"),t.toggleCrouch);
        #if ENABLE_INPUT_SYSTEM
        #else
        t.crouchKey= (KeyCode)EditorGUILayout.EnumPopup(new GUIContent("Crouch Key", "The Key used to start a crouch."),t.crouchKey);
        #endif
        t.crouchingSpeed = EditorGUILayout.Slider(new GUIContent("Crouching Speed", "How quickly can the player move while crouching?"),t.crouchingSpeed, 1, t.walkingSpeed-1);
        t.crouchingHeight = EditorGUILayout.Slider(new GUIContent("Crouching Height", "How small should the character's capsule collider be when crouching?"),t.crouchingHeight,0.01f,2);
        EditorGUILayout.EndVertical();
    
        GUI.enabled = true;

        
        EditorGUILayout.Space(20);
        GUI.enabled = false;
        t.currentStance = (Stances)EditorGUILayout.EnumPopup(new GUIContent("Current Stance", "Displays the character's current stance"),t.currentStance);
        GUI.enabled = true;
        t.stanceTransisionSpeed = EditorGUILayout.Slider(new GUIContent("Stance Transission Speed", "How quickly should the character change stances?"),t.stanceTransisionSpeed,0.1f, 10);

        EditorGUILayout.PropertyField(groundLayerMask, new GUIContent("What Is Ground", "What physics layers should be considered to be ground?"));

        #region Slope affectors
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("<color=grey>Slope Affectors</color>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleLeft,fontSize = 10, richText = true},GUILayout.ExpandWidth(true));

        t.hardSlopeLimit = EditorGUILayout.Slider(new GUIContent("Hard Slope Limit", "At what slope angle should the player no longer be able to walk up?"),t.hardSlopeLimit,45, 89);
        t.slopeInfluenceOnSpeed = EditorGUILayout.Slider(new GUIContent("Slope Influence On Speed", "How much should the slope angle influence the player's movement speed?"),t.slopeInfluenceOnSpeed, 0,5);
        t.maxStairRise = EditorGUILayout.Slider(new GUIContent("Maximum Stair Rise", "How tall can a single stair rise?"),t.maxStairRise,0,1.5f);
        EditorGUILayout.EndVertical();
        #endregion
        EditorGUILayout.EndVertical();
        #endregion

        #region Jumping
        EditorGUILayout.Space();
        GUILayout.Label("<color=grey>Jumping Settings</color>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 10, richText = true},GUILayout.ExpandWidth(true));
        EditorGUILayout.BeginVertical("box");
        //EditorGUILayout.Space(15);

        t.canJump = EditorGUILayout.ToggleLeft(new GUIContent("Can Jump", "Is the player allowed to jump?"),t.canJump);
        GUI.enabled = t.canJump;
        #if ENABLE_INPUT_SYSTEM
        #else
        t.jumpKey = (KeyCode)EditorGUILayout.EnumPopup(new GUIContent("Jump Key", "The Key used to jump."),t.jumpKey);
        #endif
        t.holdJump = EditorGUILayout.ToggleLeft(new GUIContent("Continuous Jumping", "Should the player be able to continue jumping without letting go of the Jump key"),t.holdJump);
        t.jumpPower = EditorGUILayout.Slider(new GUIContent("Jump Power", "How much power should a jump have?"),t.jumpPower,1,650f);
        t.airControlFactor = EditorGUILayout.Slider(new GUIContent("Air Control Factor", "EXPERIMENTAL: How much control should the player have over their direction while in the air"),t.airControlFactor,0,1);
        GUI.enabled = t.enableStaminaSystem;
            t.jumpingDepletesStamina = EditorGUILayout.ToggleLeft(new GUIContent("Jumping Depletes Stamina", "Should jumping deplete stamina?"),t.jumpingDepletesStamina);
            t.s_JumpStaminaDepletion = EditorGUILayout.Slider(new GUIContent("Jump Stamina Depletion Amount", "How much stamina should jumping use?"),t.s_JumpStaminaDepletion, 0, t.Stamina);
        GUI.enabled = true;
        EditorGUILayout.EndVertical();
        #endregion

        #region Sliding
        EditorGUILayout.Space();
        GUILayout.Label("<color=grey>Sliding Settings</color>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 10, richText = true},GUILayout.ExpandWidth(true));
        EditorGUILayout.BeginVertical("box");
        //EditorGUILayout.Space(15);

        t.canSlide = EditorGUILayout.ToggleLeft(new GUIContent("Can Slide", "Is the player allowed to slide? Use the crouch key to initiate a slide!"),t.canSlide);
        GUI.enabled = t.canSlide;
        t.slidingDeceleration = EditorGUILayout.Slider(new GUIContent("Sliding Deceleration", "How much deceleration should be applied while sliding?"),t.slidingDeceleration, 50,300);
        t.slidingTransisionSpeed = EditorGUILayout.Slider(new GUIContent("Sliding Transision Speed", "How quickly should the character transition from the current stance to sliding?"),t.slidingTransisionSpeed,0.01f,10);
        t.maxFlatSlideDistance = EditorGUILayout.Slider(new GUIContent("Flat Slide Distance", "If the player starts sliding on a flat surface with no ground angle influence, How many units should the player slide forward?"),t.maxFlatSlideDistance, 0.5f,15);
        GUI.enabled = true;
        EditorGUILayout.EndVertical();
        #endregion
        
        if(GUI.changed){EditorUtility.SetDirty(t); Undo.RecordObject(t,"Undo Movement Setting changes"); tSO.ApplyModifiedProperties();}
        #endregion

        #region Parkour Settings
        EditorGUILayout.Space(); EditorGUILayout.LabelField("",GUI.skin.horizontalSlider,GUILayout.MaxHeight(6)); EditorGUILayout.Space();
        GUILayout.Label("Parkour Settings",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 13},GUILayout.ExpandWidth(true));
        EditorGUILayout.Space(20);
        
        #region Vault
        EditorGUILayout.Space();
        GUILayout.Label("<color=grey>Vaulting Settings</color>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 10, richText = true},GUILayout.ExpandWidth(true));
        EditorGUILayout.BeginVertical("box");

        t.canVault = EditorGUILayout.ToggleLeft(new GUIContent("Can Vault", "Is the player allowed to vault objects?"),t.canVault);
        GUI.enabled = t.canVault;
        t.autoVaultWhenSpringing = EditorGUILayout.ToggleLeft(new GUIContent("Auto Vault While Spriting", "Should the controller automatically vault objects while sprinting?"),t.autoVaultWhenSpringing);
        if(!t.autoVaultWhenSpringing){
            #if ENABLE_INPUT_SYSTEM
            #else
            t.VaultKey = (KeyCode)EditorGUILayout.EnumPopup(new GUIContent("Vault Key", "The Key used to to vault an object"),t.VaultKey);
            #endif
        }
        t.vaultObjectTag = EditorGUILayout.TagField(new GUIContent("Vault Object Tag", "The tag required on an object to be considered vaultable."),t.vaultObjectTag);
        t.vaultSpeed = EditorGUILayout.Slider(new GUIContent("Vault Speed", "How quickly can the player vault an object?"), t.vaultSpeed, 0.1f, 15);
        t.maxVaultDepth = EditorGUILayout.Slider(new GUIContent("Maximum Vault Depth", "How deep (in meters) can a vaultable object be before it's no longer considered vaultable?"),t.maxVaultDepth, 0.1f, 3);
        t.maxVaultHeight = EditorGUILayout.Slider(new GUIContent("Maximum Vault Height", "How Tall (in meters) can a vaultable object be before it's no longer considered vaultable?"),t.maxVaultHeight, 0.1f, 3);
        EditorGUILayout.EndVertical();
        if(GUI.changed){EditorUtility.SetDirty(t); Undo.RecordObject(t,"Undo Vault Setting changes"); tSO.ApplyModifiedProperties();}
        #endregion

        #endregion

        #region Stamina
        EditorGUILayout.Space(); EditorGUILayout.LabelField("",GUI.skin.horizontalSlider,GUILayout.MaxHeight(6)); EditorGUILayout.Space();
        GUILayout.Label("Stamina",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 13},GUILayout.ExpandWidth(true));
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        t.enableStaminaSystem = EditorGUILayout.ToggleLeft(new GUIContent("Enable Stamina System", "Should the controller enable it's stamina system?"),t.enableStaminaSystem);

        //preview bar
        Rect    casingRectSP = EditorGUILayout.GetControlRect(), 
                statRectSP = new Rect(casingRectSP.x+2, casingRectSP.y+2, Mathf.Clamp(((casingRectSP.width/t.Stamina)*t.currentStaminaLevel)-4,0,casingRectSP.width), casingRectSP.height-4),
                statRectMSP = new Rect(casingRectSP.x+2, casingRectSP.y+2, Mathf.Clamp(((casingRectSP.width/t.Stamina)*t.S_minimumStaminaToSprint)-4,0,casingRectSP.width), casingRectSP.height-4);
        EditorGUI.DrawRect(casingRectSP,new Color32(64,64,64,255));
        EditorGUI.DrawRect(statRectMSP,new Color32(96,96,64,255));
        EditorGUI.DrawRect(statRectSP,new Color32(94,118,135,(byte)(GUI.enabled? 191:64)));
       
        
        GUI.enabled = t.enableStaminaSystem;
        t.Stamina = EditorGUILayout.Slider(new GUIContent("Stamina", "The maximum stamina level"),t.Stamina, 0, 250.0f);
        t.S_minimumStaminaToSprint = EditorGUILayout.Slider(new GUIContent("Minimum Stamina To Sprint", "The minimum stamina required to enter a sprint."),t.S_minimumStaminaToSprint,0,t.Stamina);
        t.s_depletionSpeed = EditorGUILayout.Slider(new GUIContent("Depletion Speed", "The speed at which stamina will depletes."),t.s_depletionSpeed,0,15.0f);
        t.s_regenerationSpeed = EditorGUILayout.Slider(new GUIContent("Regeneration Speed", "The speed at which stamina will regenerate"),t.s_regenerationSpeed, 0, 10.0f);
       
        GUI.enabled = true;
        EditorGUILayout.EndVertical();
        if(GUI.changed){EditorUtility.SetDirty(t); Undo.RecordObject(t,"Undo Stamina Setting changes"); tSO.ApplyModifiedProperties();}
        #endregion

        #region Footstep Audio
        EditorGUILayout.Space(); EditorGUILayout.LabelField("",GUI.skin.horizontalSlider,GUILayout.MaxHeight(6)); EditorGUILayout.Space();
        GUILayout.Label("Footstep Audio",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 13},GUILayout.ExpandWidth(true));
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        
        t.enableFootstepSounds = EditorGUILayout.ToggleLeft(new GUIContent("Enable Footstep System", "Should the contoller enable it's footstep audio systems?"),t.enableFootstepSounds);
        GUI.enabled = t.enableFootstepSounds;
        t.footstepTriggeringMode = (FootstepTriggeringMode)EditorGUILayout.EnumPopup(new GUIContent("Footstep Trigger Mode", "How should a footstep SFX call be triggered? \n\n- Calculated Timing: The controller will attempt to calculate the footstep cycle position based on Headbob cycle position, movement speed, and capsule size. This can sometimes be inaccuate debending on the selected perspective and base walk speed. (Not recommended if character animations are being used)\n\n- Called From Animations: The controller will not do it's own footstep cycle calculations/call for SFX. Instead the controller will rely on character Animations to call the 'CallFootstepClip()' function. This gives much more precise results. The controller will still calculate what footstep clips should be played."),t.footstepTriggeringMode);
        
        if(t.footstepTriggeringMode == FootstepTriggeringMode.calculatedTiming){
            t.stepTiming = EditorGUILayout.Slider(new GUIContent("Step Timing", "The time (measured in seconds) between each footstep."),t.stepTiming,0.0f,1.0f);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.Space();
        GUILayout.Label("<color=grey>Clip Stacks</color>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 10, richText = true},GUILayout.ExpandWidth(true));


        if(t.footstepSoundSet.Any()){
            for(int i =0; i< groundMatProf.arraySize; i++){
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginVertical("box");

                SerializedProperty profile = groundMatProf.GetArrayElementAtIndex(i), clipList = profile.FindPropertyRelative("footstepClips"), mat = profile.FindPropertyRelative("_Material"), layer = profile.FindPropertyRelative("_Layer"), triggerType = profile.FindPropertyRelative("profileTriggerType");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Clip Stack {i+1}",new GUIStyle(GUI.skin.label){fontStyle = FontStyle.Bold, fontSize = 13});
                if(GUILayout.Button(new GUIContent("X", "Remove this profile"),GUILayout.MaxWidth(20))){t.footstepSoundSet.RemoveAt(i);UpdateGroundProfiles();}
                EditorGUILayout.EndHorizontal();
                
                //Check again that the list of profiles isn't empty incase we removed the last one with the button above.
                if(t.footstepSoundSet.Any()){
                    EditorGUILayout.PropertyField(triggerType,new GUIContent("Trigger Mode", "Is this clip stack triggered by a Material or a Terrain Layer?"));
                    switch(t.footstepSoundSet[i].profileTriggerType){
                        case MatProfileType.Material:{EditorGUILayout.PropertyField(mat,new GUIContent("Material", "The material used to trigger this footstep stack."));}break;
                        case MatProfileType.terrainLayer:{EditorGUILayout.PropertyField(layer,new GUIContent("Terrain Layer", "The Terrain Layer used to trigger this footstep stack."));}break;
                    }
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(clipList,new GUIContent("Clip Stack", "The Audio clips used in this stack."),true);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space();
                    if(GUI.changed){EditorUtility.SetDirty(t); Undo.RecordObject(t,$"Undo changes to Clip Stack {i+1}"); tSO.ApplyModifiedProperties();}
                }
            }
        }
        if(GUILayout.Button(new GUIContent("Add Profile", "Add new profile"))){ t.footstepSoundSet.Add(new GroundMaterialProfile(){profileTriggerType = MatProfileType.Material, _Material = null, _Layer = null, footstepClips = new List<AudioClip>()}); UpdateGroundProfiles();}
        if(GUILayout.Button(new GUIContent("Remove All Profiles", "Remove all profiles"))){ t.footstepSoundSet.Clear();}

        //EditorGUILayout.PropertyField(groundMatProf,new GUIContent("Footstep Sound Profiles"));

        GUI.enabled = true;
        EditorGUILayout.HelpBox("Due to limitations, Imported Mesh's need to have Read/Write enabled. Errors will be thrown if it's not. Additionally Mesh's that can be walked on cannot be marked as static. Work arounds for both of these limitations are being researched.", MessageType.Info);
        EditorGUILayout.EndVertical();
        if(GUI.changed){EditorUtility.SetDirty(t); Undo.RecordObject(t,"Undo Footstep Audio Setting changes"); tSO.ApplyModifiedProperties();}

        #endregion
    
        #region Headbob
        EditorGUILayout.Space(); EditorGUILayout.LabelField("",GUI.skin.horizontalSlider,GUILayout.MaxHeight(6)); EditorGUILayout.Space();
        GUILayout.Label("Headbob",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 13},GUILayout.ExpandWidth(true));
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");

        t.enableHeadbob = EditorGUILayout.ToggleLeft(new GUIContent("Enable Headbobing", "Should the controller enable it's headbobing systems?"),t.enableHeadbob);
        GUI.enabled = t.enableHeadbob;
        t.headbobSpeed = EditorGUILayout.Slider(new GUIContent("Headbob Speed", "How fast does the headbob sway?"),t.headbobSpeed, 1.0f, 5.0f);
        t.headbobPower = EditorGUILayout.Slider(new GUIContent("Headbob Power", "How far does the headbob sway?"),t.headbobPower,1.0f,5.0f);
        t.ZTilt = EditorGUILayout.Slider(new GUIContent("Headbob Tilt", "How much does the headbob tilt at the sway extreme?"),t.ZTilt, 0.0f, 5.0f);
        
        GUI.enabled = true;
        EditorGUILayout.EndVertical();
        if(GUI.changed){EditorUtility.SetDirty(t); Undo.RecordObject(t,"Undo Headbob Setting changes"); tSO.ApplyModifiedProperties();}
        #endregion

        #region Survival Stats
        EditorGUILayout.Space(); EditorGUILayout.LabelField("",GUI.skin.horizontalSlider,GUILayout.MaxHeight(6)); EditorGUILayout.Space();
        GUILayout.Label("Survival Stats",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 13},GUILayout.ExpandWidth(true));
        EditorGUILayout.Space(10);

        SurvivalStatsTSO = new SerializedObject(t);
        defaultSurvivalStats = SurvivalStatsTSO.FindProperty("defaultSurvivalStats");
        currentSurvivalStats = SurvivalStatsTSO.FindProperty("currentSurvivalStats");
        
            #region Basic settings
            GUILayout.Label("<color=grey>Basic Settings</color>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 10, richText = true},GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginVertical("box");
            t.enableSurvivalStats = EditorGUILayout.ToggleLeft(new GUIContent("Enable Survival Stats", "Should the controller enable it's survial systems?"),t.enableSurvivalStats);
            GUI.enabled = t.enableSurvivalStats;
            t.statTickRate = EditorGUILayout.Slider(new GUIContent("Stat Ticks Per-minute", "How many times per-minute should the stats do a tick update? Each tick depletes/regenerates the stats by their respective rates below."),t.statTickRate, 0.1f, 20.0f);
            EditorGUILayout.EndVertical();
            #endregion

            #region Health Settings
            GUILayout.Label("<color=grey>Health Settings</color>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 10, richText = true},GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginVertical("box");
            SerializedProperty statHP = defaultSurvivalStats.FindPropertyRelative("Health"), currentStatHP = currentSurvivalStats.FindPropertyRelative("Health");
            
            //preview bar
            Rect casingRectHP = EditorGUILayout.GetControlRect(), statRectHP = new Rect(casingRectHP.x+2, casingRectHP.y+2, Mathf.Clamp(((casingRectHP.width/statHP.floatValue)*currentStatHP.floatValue)-4, 0, casingRectHP.width), casingRectHP.height-4);
            EditorGUI.DrawRect(casingRectHP,new Color32(64,64,64,255));
            EditorGUI.DrawRect(statRectHP,new Color32(211,0,0,(byte)(GUI.enabled? 191:64)));
           
            EditorGUILayout.PropertyField(statHP,new GUIContent("Health Points", "How much health does the controller start with?"));
           
            GUI.enabled = false;
            EditorGUILayout.ToggleLeft(new GUIContent("Health is critically low?"),currentSurvivalStats.FindPropertyRelative("hasLowHealth").boolValue);
            GUI.enabled = t.enableSurvivalStats;
            EditorGUILayout.EndVertical();
            #endregion

            #region Hunger Settings
            GUILayout.Label("<color=grey>Hunger Settings</color>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 10, richText = true},GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginVertical("box");
            SerializedProperty statHU = defaultSurvivalStats.FindPropertyRelative("Hunger"), currentStatHU = currentSurvivalStats.FindPropertyRelative("Hunger");
            
            //preview bar
            Rect casingRectHU = EditorGUILayout.GetControlRect(), statRectHU = new Rect(casingRectHU.x+2, casingRectHU.y+2, Mathf.Clamp(((casingRectHU.width/statHU.floatValue)*currentStatHU.floatValue)-4,0,casingRectHU.width), casingRectHU.height-4);
            EditorGUI.DrawRect(casingRectHU,new Color32(64,64,64,255));
            EditorGUI.DrawRect(statRectHU,new Color32(255,194,0,(byte)(GUI.enabled? 191:64)));
           
            EditorGUILayout.PropertyField(statHU,new GUIContent("Hunger Points", "How much Hunger does the controller start with?"));
            t.hungerDepletionRate = EditorGUILayout.Slider(new GUIContent("Hunger Depletion Per Tick","How much does the hunger deplete per tick?"), t.hungerDepletionRate,0,5);
            GUI.enabled = false;
            EditorGUILayout.ToggleLeft(new GUIContent("Player is Starving?"),currentSurvivalStats.FindPropertyRelative("isStarving").boolValue);
            GUI.enabled = t.enableSurvivalStats;
            EditorGUILayout.EndVertical();
            #endregion

            #region Hydration Settings
            GUILayout.Label("<color=grey>Hydration Settings</color>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 10, richText = true},GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginVertical("box");
            SerializedProperty statHY = defaultSurvivalStats.FindPropertyRelative("Hydration"), currentStatHY = currentSurvivalStats.FindPropertyRelative("Hydration");
            
            //preview bar
            Rect casingRectHY = EditorGUILayout.GetControlRect(), statRectHY = new Rect(casingRectHY.x+2, casingRectHY.y+2,Mathf.Clamp(((casingRectHY.width/statHY.floatValue)*currentStatHY.floatValue)-4, 0, casingRectHY.width), casingRectHY.height-4);
            EditorGUI.DrawRect(casingRectHY,new Color32(64,64,64,255));
            EditorGUI.DrawRect(statRectHY,new Color32(0,194,255,(byte)(GUI.enabled? 191:64)));
            
            EditorGUILayout.PropertyField(statHY,new GUIContent("Hydration Points", "How much Hydration does the controller start with?"));
            t.hydrationDepletionRate = EditorGUILayout.Slider(new GUIContent("Hydration Depletion Per Tick","How much does the hydration deplete per tick?"), t.hydrationDepletionRate,0,5);
            GUI.enabled = false;
            EditorGUILayout.ToggleLeft(new GUIContent("Player is Dehydrated?"),currentSurvivalStats.FindPropertyRelative("isDehydrated").boolValue);
            GUI.enabled = t.enableSurvivalStats;
            EditorGUILayout.EndVertical();
            #endregion


        GUI.enabled = true;
        if(GUI.changed){EditorUtility.SetDirty(t); Undo.RecordObject(t,"Undo Survival Stat Setting changes"); tSO.ApplyModifiedProperties();}
        #endregion
    
        #region Animation Triggers
        EditorGUILayout.Space(); EditorGUILayout.LabelField("",GUI.skin.horizontalSlider,GUILayout.MaxHeight(6)); EditorGUILayout.Space();
        GUILayout.Label("Animator Settup",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 13},GUILayout.ExpandWidth(true));
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginVertical("box");
        t._1stPersonCharacterAnimator = (Animator)EditorGUILayout.ObjectField(new GUIContent("1st Person Animator", "The animator used on the 1st person character mesh (if any)"),t._1stPersonCharacterAnimator,typeof(Animator), true);
        t._3rdPersonCharacterAnimator = (Animator)EditorGUILayout.ObjectField(new GUIContent("3rd Person Animator", "The animator used on the 3rd person character mesh (if any)"),t._3rdPersonCharacterAnimator,typeof(Animator), true);
        EditorGUILayout.HelpBox("WIP - This is a work in progress feature and currently very primitive.\n\n No triggers, bools, floats, or ints are set up in the script. To utilize this feature, find 'UpdateAnimationTriggers()' function in this script and set up triggers with the correct string names there. This function gets called by the script whenever a relevant parameter gets updated. (I.e. when 'isVaulting' changes)" ,MessageType.Info);
        EditorGUILayout.EndVertical();
        if(GUI.changed){EditorUtility.SetDirty(t); Undo.RecordObject(t,"Undo Animation settings changes"); tSO.ApplyModifiedProperties();}
        #endregion

        #region Debuggers
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("",GUI.skin.horizontalSlider,GUILayout.MaxHeight(6));EditorGUILayout.Space(); 
        GUILayout.Label("<color=grey>Debuggers</color>",new GUIStyle(GUI.skin.label){alignment = TextAnchor.MiddleCenter,fontStyle = FontStyle.Bold, fontSize = 10, richText = true},GUILayout.ExpandWidth(true));
        EditorGUILayout.BeginVertical("box");
        
        float maxWidth = (EditorGUIUtility.currentViewWidth/2)-20;
        EditorGUILayout.BeginHorizontal();
        t.enableGroundingDebugging = GUILayout.Toggle(t.enableGroundingDebugging, "Debug Grounding System","Button", GUILayout.Width(maxWidth));
        t.enableMovementDebugging = GUILayout.Toggle(t.enableMovementDebugging, "Debug Movement System","Button", GUILayout.Width(maxWidth));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        t.enableMouseAndCameraDebugging = GUILayout.Toggle(t.enableMouseAndCameraDebugging,"Debug Mouse and Camera","Button", GUILayout.Width(maxWidth));
        t.enableVaultDebugging = GUILayout.Toggle(t.enableVaultDebugging, "Debug Vault System","Button", GUILayout.Width(maxWidth));
        EditorGUILayout.EndHorizontal();
    
        if(t.enableGroundingDebugging || t.enableMovementDebugging || t.enableMouseAndCameraDebugging || t.enableVaultDebugging){
            EditorGUILayout.HelpBox("Debuggers can cause lag! Even in Application builds, make sure to keep these switched off unless absolutely necessary!",MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
        if(GUI.changed){EditorUtility.SetDirty(t); Undo.RecordObject(t,"Undo Debugger changes"); tSO.ApplyModifiedProperties();}
        #endregion
    }

    void UpdateGroundProfiles(){
        tSO = new SerializedObject(t);
        groundMatProf = tSO.FindProperty("footstepSoundSet");
    }
}
#endif
#endregion
