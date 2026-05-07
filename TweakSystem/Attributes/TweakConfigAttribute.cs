using System;
using JetBrains.Annotations;

namespace BryerTweaks.TweakSystem;

[AttributeUsage(AttributeTargets.Property)]
[MeansImplicitUse(ImplicitUseKindFlags.Access | ImplicitUseKindFlags.Assign)]
public class TweakConfigAttribute : Attribute;
