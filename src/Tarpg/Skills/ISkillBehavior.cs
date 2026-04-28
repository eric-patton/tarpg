namespace Tarpg.Skills;

// The behavior of a skill: what happens when it fires.
// Implementations can be stateless (recommended) or hold static config.
public interface ISkillBehavior
{
    void Execute(SkillContext context);
}
