﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Xamarin.Forms;
using Debug = System.Diagnostics.Debug;

namespace Plugin.SharedTransitions
{
    /// <summary>
    /// TransitionMapper implementation
    /// </summary>
    /// <seealso cref="ITransitionMapper" />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class TransitionMapper : ITransitionMapper
    {
        readonly Lazy<List<TransitionMap>> _transitionStack = new Lazy<List<TransitionMap>>(() => new List<TransitionMap>());

        public IReadOnlyList<TransitionMap> TransitionStack => _transitionStack.Value;

        public IReadOnlyList<TransitionDetail> GetMap(Page page, string selectedGroup, bool ignoreGroup = false)
        {
            if (ignoreGroup)
                return TransitionStack.Where(x => x.PageId == page.Id)
                           .Select(x => x.Transitions.ToList())
                           .FirstOrDefault() ?? new List<TransitionDetail>();

            return TransitionStack.Where(x => x.PageId == page.Id)
                           .Select(x => x.Transitions.Where(tr=>tr.TransitionGroup == selectedGroup).ToList())
                           .FirstOrDefault() ?? new List<TransitionDetail>();
        }

        public int AddOrUpdate(Page page, string transitionName, string transitionGroup, Guid formsViewId, int nativeViewId)
        {
            var transitionMap = _transitionStack.Value.FirstOrDefault(x => x.PageId == page.Id);

            if (transitionMap == null)
            {
                if (nativeViewId == 0) 
                    nativeViewId = 1;

                _transitionStack.Value.Add(
                    new TransitionMap
                    {
                        PageId = page.Id,
                        Transitions   = new List<TransitionDetail> {CreateTransition(transitionName, transitionGroup, formsViewId, nativeViewId)}
                    }
                );

                return nativeViewId;
            }

            var transitionDetail = transitionMap.Transitions.FirstOrDefault(x => x.FormsViewId == formsViewId);
            if (transitionDetail == null)
            {
                //In iOS we dont have autogenerated IDs, let's create one!
                if (nativeViewId == 0)
                    nativeViewId = (transitionMap.Transitions.OrderBy(x => x.NativeViewId).LastOrDefault()?.NativeViewId ?? 0) + 1;

                transitionMap.Transitions.Add(CreateTransition(transitionName, transitionGroup, formsViewId, nativeViewId));
            }
            else
            {
                //the transition already exists lets check if the mapping is correct
                if (nativeViewId == 0)
                {
                    //common in ios
                    nativeViewId = transitionDetail.NativeViewId; 
                }
                else if (transitionDetail.NativeViewId != nativeViewId)
                {
                    transitionDetail.NativeViewId = nativeViewId; //the nativeId in the stack is different
                    Debug.WriteLine($"NativeviewId for transition {transitionName} should not be different in the stack at this point! Forms Guid is {formsViewId}");
                }

                if (transitionDetail.TransitionName  != transitionName ||
                    transitionDetail.TransitionGroup != transitionGroup && transitionGroup != null)
                {
                    transitionDetail.TransitionName  = transitionName;
                    transitionDetail.TransitionGroup = transitionGroup;

                    /*
                     * IMPORTANT
                     *
                     * This is where i should clean the mapstack for dynamic transitions
                     * where the items are being attached and detached by virtualization.
                     * Unfortunately doing this here where i'm in the "reign" of attached properties and binding
                     * will result in a corrupted mapstack (i suppose due the choppiness of bindings)
                     *
                     * The only way i found is to clear the mapstack in the native renderer when i find orphaned ids
                     */

                    /* OLD CODE:
                     
                    var alreadyexisting = transitionMap.Transitions.Where(x =>
                    x.TransitionName == transitionName && x.TransitionGroup == transitionGroup &&
                    x.FormsViewId != formsViewId).ToList();

                    for (int i = alreadyexisting.Count - 1; i >= 0; i--)
                        transitionMap.Transitions.RemoveAt(i);*/
                }
            }

            return nativeViewId;
        }

        public void Remove(Page page, Guid formsViewId)
        {
            var transitionMap = _transitionStack.Value.FirstOrDefault(x=>x.PageId == page.Id);
            transitionMap?.Transitions.Remove(transitionMap.Transitions.FirstOrDefault(x=>x.FormsViewId == formsViewId));
        }

        public void Remove(Page page, int nativeViewId)
        {
            var transitionMap = _transitionStack.Value.FirstOrDefault(x=>x.PageId == page.Id);
            transitionMap?.Transitions.Remove(transitionMap.Transitions.FirstOrDefault(x=>x.NativeViewId == nativeViewId));
        }

        public void RemoveFromPage(Page page)
        {
            _transitionStack.Value.Remove(_transitionStack.Value.FirstOrDefault(x => x.PageId == page.Id));
        }

        public TransitionDetail CreateTransition(string transitionName,string transitionGroup, Guid formsViewId, int nativeViewId)
        {
            return new TransitionDetail
            {
                TransitionName  = transitionName,
                TransitionGroup = transitionGroup,
                FormsViewId     = formsViewId,
                NativeViewId    = nativeViewId
            };
        }
    }
}
