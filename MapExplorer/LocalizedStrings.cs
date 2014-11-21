/*
 * Copyright (c) 2012-2014 Microsoft Mobile. All rights reserved.
 * See the license file delivered with this project for more information.
 */

using MapExplorer.Resources;

namespace MapExplorer
{
    /// <summary>
    /// Provides access to string resources.
    /// </summary>
    public class LocalizedStrings
    {
        private static AppResources _localizedResources = new AppResources();

        public AppResources LocalizedResources { get { return _localizedResources; } }
    }
}