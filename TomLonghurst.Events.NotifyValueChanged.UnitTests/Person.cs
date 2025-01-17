﻿using System.Globalization;
using TomLonghurst.Events.NotifyValueChanged.SourceGeneration.Attributes;
using TomLonghurst.Events.NotifyValueChanged.SourceGeneration.Options;

namespace TomLonghurst.Events.NotifyValueChanged.UnitTests;

[NotifyTypeValueChange(typeof(string))]
[NotifyTypeValueChange(typeof(int))]
[NotifyAnyValueChange]
public partial class Person
{
    [NotifyValueChange]
    private string _firstName;

    [NotifyValueChange(GetterAccessLevel = PropertyAccessLevel.PrivateProtected, SetterAccessLevel = PropertyAccessLevel.Internal)]
    private string _middleName;
    
    [NotifyValueChange(PropertyName = "FamilyName")]
    private string _lastName;

    [NotifyValueChange]
    private int _age;

    public string FullName => $"{_firstName} {MiddleName} {_lastName}";

    public string AgeInYears => $"{Age} years old";

    public string Description => $"{FullName} is {AgeInYears}";

    public string UppercaseDescription => Description.ToUpper(CultureInfo.CurrentCulture);
}