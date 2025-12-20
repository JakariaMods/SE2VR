using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SE2VR.Client.Input;

/// <summary>
/// Interface for object that is used to determine if an action set should be active. For example, the menu action set should not be active when menus are closed.
/// </summary>
public interface IActionSetContext
{
    /// <summary>
    /// If the action set should be accepting inputs
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Recompute <see cref="IsActive"/>
    /// </summary>
    void UpdateActive();
}
