namespace Pdlc.Domain.Enums;

public enum PdlcStage
{
    Requirements = 1,
    Design       = 2,
    CodeGen      = 3,
    TestGen      = 4,
    PrReview     = 5
}

public enum RequirementStatus
{
    Draft     = 0,
    Analyzed  = 1,
    Approved  = 2,
    InDesign  = 3,
    InDev     = 4,
    Done      = 5
}

public enum DesignArtifactType
{
    ComponentTree = 1,
    ApiContract   = 2,
    DataModel     = 3
}

public enum CodeGenLanguage
{
    CSharp  = 1,
    Angular = 2,
    Both    = 3
}

public enum TestGenTarget
{
    DotNet  = 1,
    Angular = 2,
    Both    = 3
}

public enum AdoWorkItemType
{
    UserStory = 1,
    Task      = 2,
    Bug       = 3,
    Epic      = 4
}
