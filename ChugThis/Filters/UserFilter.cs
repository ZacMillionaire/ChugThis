using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nulah.ChugThis.Models.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Filters {
    public class UserFilter : ActionFilterAttribute {

        private readonly bool _RequiredLoggedInState;
        private readonly string _RedirectOnFail;
        /// <summary>
        ///     <para>
        /// Any PublicUser must have an isLoggedIn state to match the RequiredLoggedInState given. If not, they will be redirected
        /// to the redirect given.
        ///     </para><para>
        /// Redirect will default to ~/ if no value is given.
        ///     </para>
        /// </summary>
        /// <param name="MustBeAuthorised"></param>
        public UserFilter(bool RequiredLoggedInState, string Redirect = "~/") {
            _RequiredLoggedInState = RequiredLoggedInState;
            _RedirectOnFail = Redirect;
        }

        [ServiceFilter(typeof(PublicUser))]
        public override void OnActionExecuting(ActionExecutingContext context) {

            // This isn't necessarily a real user, it could be a blank PublicUser that only has IsLoggedIn set to false
            // If the instance IsLoggedIn is true however, we have a legitimate user account
            Controller BaseController = (Controller)context.Controller;
            PublicUser CurrentUserInstance = (PublicUser)BaseController.ViewData["User"];

            if(CurrentUserInstance.isLoggedIn != _RequiredLoggedInState) {
                context.Result = new LocalRedirectResult(_RedirectOnFail);
            }

            base.OnActionExecuting(context);
        }
    }
}
