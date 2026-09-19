using JapanMarket.Domain;

namespace JapanMarket.Gameplay
{

    public abstract class CustomerStateBase : IState<CustomerContext>
    {
        public virtual void Enter(CustomerContext context) { }
        public virtual void Tick(CustomerContext context, float deltaTime) { }
        public virtual void Exit(CustomerContext context) { }

        protected static void HaltFacing(CustomerContext context, UnityEngine.Vector3 lookAt)
        {
            if (!context.Locomotion.IsHalted) context.Locomotion.Halt();
            context.Locomotion.FacePoint(lookAt);
        }
    }
}
